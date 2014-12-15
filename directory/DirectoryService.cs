using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OnionRouting
{
    class DirectoryService
    {
        const int PORT = 8000;
        const int CHAIN_LENGTH = 3;
        const int TARGET_CHAIN_NODE_COUNT = 6;
        const int ALIVE_CHECK_INTERVAL = 5000;
        const int ALIVE_CHECK_TIMEOUT = 4000;

        static List<ChainNodeInfo> _runningChainNodes = new List<ChainNodeInfo>();
        static List<ChainNodeInfo> _startingChainNodes = new List<ChainNodeInfo>();
        
        static Random _rng = new Random();

        static void Main(string[] args)
        {
            discoverChainNodes();

            new Thread(checkRunningChainNodeStatus).Start();
            HttpListener listener = Messaging.createListener(PORT, false, "chain");

            Log.info("directory node up and running (port {0})", PORT);
            
            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerResponse response = context.Response;

				if (context.Request.HttpMethod != "GET")
				{
					response.StatusCode = Messaging.HTTP_METHOD_NOT_ALLOWED;
					response.Close();
					continue;
				}

                Log.info("handing incoming chain request from {0}", context.Request.RemoteEndPoint);

                StringBuilder responseData = new StringBuilder();

                var chain = getRandomChain();
                if (chain == null)
                {
					response.StatusCode = Messaging.HTTP_SERVICE_UNAVAILABLE;
                    responseData.AppendLine("Not enough chain nodes available!");
                }
                else
                    foreach (var chainNode in chain)
                    {
                        responseData.AppendLine(chainNode.Url + "/route");
                        responseData.AppendLine(chainNode.PublicKeyXml);
                    }

                byte[] buffer = Encoding.UTF8.GetBytes(responseData.ToString());

                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();
            }
        }

        //static void discoverChainNodes()
        //{
        //    Log.info("discovering chain nodes");

        //    for (int i = 9000; i < 9006; i++)
        //    {
        //        bool success;
        //        string url = "http://localhost:" + i;

        //        byte[] response = Messaging.sendRecv(url + "/key", out success);

        //        if (success)
        //        {
        //            string xml = Encoding.UTF8.GetString(response);
        //            _runningChainNodes.Add(new ChainNodeData(url, xml));

        //            Log.info("chain node at " + url.Substring(7) + " discovered");
        //        }
        //        else
        //            Log.error("chain node at " + url.Substring(7) + " unavailable");
        //    }
        //}
        
        static IEnumerable<ChainNodeInfo> getRandomChain()
        {
            lock (_runningChainNodes)
            {
                if (_runningChainNodes.Count < CHAIN_LENGTH)
                    return null;

                return _runningChainNodes.OrderBy(x => _rng.Next()).Take(CHAIN_LENGTH);
            }
        }

        static void discoverChainNodes()
        {
            Log.info("discovering running chain nodes");
			AWSHelper awsHelper = AWSHelper.instance();
			_startingChainNodes.AddRange(awsHelper.discoverChainNodes());

            for (int i = _startingChainNodes.Count; i < TARGET_CHAIN_NODE_COUNT; i++)
            {
                Log.info("launching new chain node");
				_startingChainNodes.Add(awsHelper.launchNewChainNodeInstance());
            }

            new Thread(waitForNewChainNodes).Start();
        }
        
        static void checkRunningChainNodeStatus()
        {
			AWSHelper awsHelper = AWSHelper.instance();

            while (true)
            {
                Task<WebResponse>[] tasks = new Task<WebResponse>[_runningChainNodes.Count];

                for (int i = 0; i < _runningChainNodes.Count; i++)
                {
                    tasks[i] = HttpWebRequest.CreateHttp(_runningChainNodes[i].Url + "/status").GetResponseAsync();
                }
                                
                Thread.Sleep(ALIVE_CHECK_TIMEOUT);
                
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
                        Log.info("chain node at {0} gone offline!", _runningChainNodes[j].Url.Substring(7));
                        lock (_runningChainNodes)
                        {
                            _runningChainNodes.RemoveAt(j);
                            j--;
                        }
                    }

                    j++;
                }

                Log.info("status of all chain nodes checked");
                Thread.Sleep(ALIVE_CHECK_INTERVAL - ALIVE_CHECK_TIMEOUT);


                // start new chain node instances if there are not 6 available/starting
                lock (_runningChainNodes)
                    lock (_startingChainNodes)
                        try
                        {
                            while (_runningChainNodes.Count + _startingChainNodes.Count < TARGET_CHAIN_NODE_COUNT)
                            {
						_startingChainNodes.Add(awsHelper.launchNewChainNodeInstance());
                                Log.info("started new chain node instance");
                            }
                        }
                        catch {
                            Log.error("failed to start new chain node instance (AWS instance limit)");
                        }
            }
        }
                            
        static void waitForNewChainNodes()
        {
			AWSHelper awsHelper = AWSHelper.instance();
            while (true)
            {
                ChainNodeInfo[] startingChainNodes;
                lock (_startingChainNodes)
                    startingChainNodes = _startingChainNodes.ToArray();

                for (int i = 0; i < startingChainNodes.Length; i++)
                {
					if (awsHelper.checkChainNodeState(startingChainNodes[i]) == "running")
                    {
                        string url = "http://" + startingChainNodes[i].IP + ":" + PORT;
                        startingChainNodes[i].Url = url;
                        bool success;
                        byte[] response = Messaging.sendRecv(startingChainNodes[i].Url + "/key", out success);
                        if (success)
                        {
                            string xml = Encoding.UTF8.GetString(response);
                            startingChainNodes[i].PublicKeyXml = xml;

                            lock (_startingChainNodes)
                                _startingChainNodes.Remove(startingChainNodes[i]);

                            lock (_runningChainNodes)
                                _runningChainNodes.Add(startingChainNodes[i]);

                            Log.info("chain node at " + startingChainNodes[i].IP + " up and running");
                        }
                    }
                }

                Thread.Sleep(ALIVE_CHECK_INTERVAL);
            }
        }
    }
}
