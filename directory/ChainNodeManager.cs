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
		const int NODE_PORT = 8000;
		const int CHAIN_LENGTH = 3;
		const int TARGET_CHAIN_NODE_COUNT = 6;
		const int ALIVE_CHECK_INTERVAL = 5000;
		const int ALIVE_CHECK_TIMEOUT = 4000;

		public bool AutoStartChainNodes { get; set; }
		private bool running = false;

		private List<ChainNodeInfo> runningChainNodes = new List<ChainNodeInfo>();
		private List<ChainNodeInfo> startingChainNodes = new List<ChainNodeInfo>();
		private Random rng = new Random();

		private ManualResetEvent stopThreadsEvent = new ManualResetEvent(false);
		private Thread runningStatusThread = null;
		private Thread newNodesThread = null;

		public ChainNodeManager()
		{
			AutoStartChainNodes = false;
		}

		public IEnumerable<ChainNodeInfo> getRandomChain()
		{
			lock (runningChainNodes)
			{
				if (runningChainNodes.Count < CHAIN_LENGTH)
					return null;

				return runningChainNodes.OrderBy(x => rng.Next()).Take(CHAIN_LENGTH);
			}
		}

		public void discoverChainNodes()
		{
			Log.info("discovering running chain nodes");
			AWSHelper awsHelper = AWSHelper.instance();
			lock (startingChainNodes)
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

		private void checkrunningChainNodeStatus()
		{
			AWSHelper awsHelper = AWSHelper.instance();

			while (running)
			{
				Stopwatch stopwatch = Stopwatch.StartNew();

				Task<WebResponse>[] tasks;
				lock (runningChainNodes)
				{
					tasks = new Task<WebResponse>[runningChainNodes.Count];

					for (int i = 0; i < runningChainNodes.Count; i++)
					{
						tasks[i] = HttpWebRequest.CreateHttp(runningChainNodes[i].Url + "/status").GetResponseAsync();
					}
				}

				// wait until all tasks have completed or the timeout has been reached.
				Task.WaitAll(tasks, ALIVE_CHECK_TIMEOUT);
//				foreach (Task<WebResponse> task in tasks)
//				{
//					long timeoutWaitTime = ALIVE_CHECK_TIMEOUT - stopwatch.ElapsedMilliseconds;
//					if (timeoutWaitTime > 0)
//						task.Wait(timeoutWaitTime);
//				}

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
						lock (runningChainNodes)
						{
							Log.info("chain node at {0} gone offline!", runningChainNodes[j].Url.Substring(7));
							runningChainNodes.RemoveAt(j);
							j--;
						}
					}

					j++;
				}

				Log.info("status of all chain nodes checked");

				if (AutoStartChainNodes)
				{
					// start new chain node instances if there are not 6 available/starting
					lock (runningChainNodes)
					lock (startingChainNodes)
						try
					{
						while (runningChainNodes.Count + startingChainNodes.Count < TARGET_CHAIN_NODE_COUNT)
						{
							startingChainNodes.Add(awsHelper.launchNewChainNodeInstance());
							Log.info("started new chain node instance");
						}
					}
					catch {
						Log.error("failed to start new chain node instance (AWS instance limit)");
					}
				}

				// wait until our interval has passed or we are explicitly woken up to stop the thread
				stopwatch.Stop();
				int waitTime = ALIVE_CHECK_INTERVAL - (int)stopwatch.ElapsedMilliseconds;
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

				ChainNodeInfo[] startingNodes;
				lock (startingChainNodes)
					startingNodes = startingChainNodes.ToArray();

				for (int i = 0; i < startingNodes.Length; i++)
				{
					if (awsHelper.checkChainNodeState(startingNodes[i]) == "running")
					{
						string url = "http://" + startingNodes[i].IP + ":" + NODE_PORT;
						startingNodes[i].Url = url;
						bool success;
						byte[] response = Messaging.sendRecv(startingNodes[i].Url + "/key", out success);
						if (success)
						{
							string xml = Encoding.UTF8.GetString(response);
							startingNodes[i].PublicKeyXml = xml;

							lock (startingChainNodes)
								startingChainNodes.Remove(startingNodes[i]);

							lock (runningChainNodes)
								runningChainNodes.Add(startingNodes[i]);

							Log.info("chain node at " + startingNodes[i].IP + " up and running");
						}
					}
				}

				// wait until our interval has passed or we are explicitly woken up to stop the thread
				stopwatch.Stop();
				int waitTime = ALIVE_CHECK_INTERVAL - (int)stopwatch.ElapsedMilliseconds;
				if (waitTime > 0)
					stopThreadsEvent.WaitOne(waitTime);
			}
		}
	}
}
