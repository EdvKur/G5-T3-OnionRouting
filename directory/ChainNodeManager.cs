using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Net;
using System.Linq;
using System.Diagnostics;

namespace OnionRouting
{
	/// <summary>
	/// Maintains a list of available chain nodes and tests periodically id they are still available.
	/// Call start() to start the thread(s) that update the list and stop() when you're done.
	/// </summary>
	public class ChainNodeManager
	{
		private int chainLength;
		private int targetChainNodeCount;
		private int aliveCheckInterval;
		private int aliveCheckTimeout;

        private bool autoStartChainNodes;
		private bool running = false;

		// After the start command has been sent to EC2, the node is considered "starting".
		// It is "running" once EC2 has reported that it finished booting.
		// After the node has actually responded to a status request, it is "ready".
		private List<ChainNodeInfo> startingChainNodes;
		private List<ChainNodeInfo> runningChainNodes;
		private List<ChainNodeInfo> readyChainNodes;
		private Random rng;

        private Dictionary<string, int> usage;

		private ManualResetEvent stopThreadsEvent = new ManualResetEvent(false);
		private Thread runningStatusThread = null;
		private Thread newNodesThread = null;

		public ChainNodeManager()
		{
            autoStartChainNodes = Properties.Settings.Default.autoStartChainNodes;

            chainLength = Properties.Settings.Default.chainLength;
            targetChainNodeCount = Properties.Settings.Default.targetChainNodeCount;
            aliveCheckInterval = Properties.Settings.Default.aliveCheckInterval;
            aliveCheckTimeout = Properties.Settings.Default.aliveCheckTimeout;

            startingChainNodes = new List<ChainNodeInfo>();
		    runningChainNodes = new List<ChainNodeInfo>();
		    readyChainNodes = new List<ChainNodeInfo>();
		    rng = new Random();
		}

		public IEnumerable<ChainNodeInfo> getChain(string strategy)
		{
			lock (readyChainNodes)
			{
                if (readyChainNodes.Count < chainLength)
                {
                    return null;
                } else if (strategy == "random")
                {
                    return readyChainNodes.OrderBy(x => rng.Next()).Take(chainLength);
                } else if (strategy == "balanced")
                {
                    var minUsage = readyChainNodes.OrderBy(x => x.usageCount).First().usageCount;
                    Log.info("minimum # of usages: {0}", minUsage);
                    List<ChainNodeInfo> response = new List<ChainNodeInfo>();

                    while (response.Count < chainLength)
                    {    
                        var minNodes = readyChainNodes.FindAll(
                            delegate(ChainNodeInfo info)
                            {
                                return info.usageCount == minUsage;
                            }
                            );

                        if (minNodes.Count > chainLength) {
                            response.AddRange(minNodes.OrderBy(x => rng.Next()).Take(chainLength));
                        } else {
                            response.AddRange(minNodes);
                        }

                        minUsage++;
                    }

                    foreach (ChainNodeInfo node in response) {
                        node.usageCount++;
                        Log.info("chain node at {0} with usage count {1} added to chain", node.IP, node.usageCount);
                    }

                    return response;
                }
                else
                {
                    throw new ArgumentException("Invalid loadbalancing strategy.");
                }
			}
		}

		public void discoverChainNodes()
		{
			Log.info("discovering running chain nodes");
			AWSHelper awsHelper = AWSHelper.instance();
			//lock (startingChainNodes)
				startingChainNodes.AddRange(awsHelper.discoverChainNodes());
		}

		public void start()
		{
			if (running)
				return;

			running = true;
			runningStatusThread = new Thread(checkrunningChainNodeStatus);
			newNodesThread = new Thread(waitForNewChainNodes);
			runningStatusThread.Start();
			newNodesThread.Start();
		}

		public void stop()
		{
			running = false;
			stopThreadsEvent.Set();
			if (runningStatusThread != null)
			{
				runningStatusThread.Join();
				runningStatusThread = null;
			}

			if (newNodesThread != null)
			{
				newNodesThread.Join();
				newNodesThread = null;
			}
			stopThreadsEvent.Reset();
		}

		public bool isrunning()
		{
			return running;
		}

		public void addManualChainNode(ChainNodeInfo node)
		{
			lock (runningChainNodes)
				runningChainNodes.Add(node);
		}

		public int countReadyNodes()
		{
			lock (readyChainNodes)
				return readyChainNodes.Count;
		}

		private void checkrunningChainNodeStatus()
		{
			while (running)
			{
                Stopwatch stopwatch = Stopwatch.StartNew();

                lock (DirectoryService.lockObj)
                {
                
                    Task<WebResponse>[] tasks;
                    {
                        tasks = new Task<WebResponse>[readyChainNodes.Count];

                        for (int i = 0; i < readyChainNodes.Count; i++)
                        {
                            tasks[i] = HttpWebRequest.CreateHttp(readyChainNodes[i].Url + "/status").GetResponseAsync();
                        }
                    }

                    // wait until all tasks have completed or the timeout has been reached.
                    Task.WaitAll(tasks, aliveCheckTimeout);

                    int j = 0;
                    for (int i = 0; i < tasks.Length; i++)
                    {
                        if (tasks[i].IsCompleted)
                        {
                            tasks[i].Result.Close();
                            tasks[i].Dispose();
                        }
                        else
                        {
                            Log.info("chain node at {0} gone offline!", readyChainNodes[j].Url.Substring(7));
                            readyChainNodes.RemoveAt(j);
                            j--;
                        }

                        j++;
                    }

                    Log.info("status of all {0} ready chain nodes checked", readyChainNodes.Count);
                }

                if (autoStartChainNodes)
                {
                    lock (DirectoryService.lockObj)
                    {
                        AWSHelper awsHelper = AWSHelper.instance();

                        // start new chain node instances if there are not 6 available/starting
                        try
                        {
                            while (readyChainNodes.Count + startingChainNodes.Count + runningChainNodes.Count < targetChainNodeCount)
                            {
                                startingChainNodes.Add(awsHelper.launchNewChainNodeInstance());
                                Log.info("started chain node instance #{0}", startingChainNodes.Count);
                            }
                        }
                        catch
                        {
                            Log.error("failed to start new chain node instance (AWS instance limit)");
                        }
                    }
                }

				// wait until our interval has passed or we are explicitly woken up to stop the thread
				stopwatch.Stop();
				int waitTime = aliveCheckInterval - (int)stopwatch.ElapsedMilliseconds;
				if (waitTime > 0)
					stopThreadsEvent.WaitOne(waitTime);
			}
		}

		private void waitForNewChainNodes()
		{
			AWSHelper awsHelper = AWSHelper.instance();

			while (running)
			{
				Stopwatch stopwatch = Stopwatch.StartNew();

				// starting -> running
				//ChainNodeInfo[] startingNodes;
                //lock (startingChainNodes)
                //{
                    //startingNodes = startingChainNodes.ToArray();
                    //				Log.info("starting nodes: " + startingNodes.Length);
                lock (DirectoryService.lockObj)
                {
                    for (int i = 0; i < startingChainNodes.Count; i++)
                    {
                        if (awsHelper.checkChainNodeState(startingChainNodes[i]) == "running")
                        {
                            string url = "http://" + startingChainNodes[i].IP + ":" + startingChainNodes[i].port;
                            startingChainNodes[i].Url = url;

                            runningChainNodes.Add(startingChainNodes[i]);
                            Log.info("chain node at " + startingChainNodes[i].IP + " running");

                            startingChainNodes.Remove(startingChainNodes[i]);
                        }
                    }

                }
            
                // running -> ready
                for (int i = 0; i < runningChainNodes.Count; i++)
                {
                    bool success;
                    byte[] response = Messaging.sendRecv(runningChainNodes[i].Url + "/key", out success);
                    if (success)
                    {
                        string xml = Encoding.UTF8.GetString(response);
                        runningChainNodes[i].PublicKeyXml = xml;

                        lock (DirectoryService.lockObj)
                        {
                            int avg = 0;
                            int count = 0;
                            foreach (ChainNodeInfo node in runningChainNodes) {
                                avg += node.usageCount;
                                count++;
                            }
                            avg = avg / count;

                            readyChainNodes.Add(runningChainNodes[i]);
                            Log.info("chain node at " + runningChainNodes[i].IP + " ready, usage set at average of {0}", avg);

                            runningChainNodes.Remove(runningChainNodes[i]);
                        }
                    }
                    else
                    {
                        Log.info("chain node at " + runningChainNodes[i].IP + " not yet ready");
                    }
                }
                

				// wait until our interval has passed or we are explicitly woken up to stop the thread
				stopwatch.Stop();
				int waitTime = aliveCheckInterval - (int)stopwatch.ElapsedMilliseconds;
				if (waitTime > 0)
					stopThreadsEvent.WaitOne(waitTime);
			}
		}
	}
}
