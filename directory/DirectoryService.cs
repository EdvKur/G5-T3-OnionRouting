using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace OnionRouting
{
	public class DirectoryService : OnionService
    {
		static int defaultPort;
        static string url;
        public static Object lockObj = new Object();

		private ChainNodeManager chainNodeManager;

        static DirectoryService()
        {
            defaultPort = Properties.Settings.Default.defaultPort;
            url = Properties.Settings.Default.url;
        }

		public DirectoryService(int port)
			: base(port)
		{
            chainNodeManager = new ChainNodeManager();
		}

		public void discoverChainNodes()
		{
			chainNodeManager.discoverChainNodes();
		}

		public void addManualChainNode(ChainNodeInfo node)
		{
			chainNodeManager.addManualChainNode(node);
		}

		public int countReadyNodes()
		{
			if (chainNodeManager != null)
				return chainNodeManager.countReadyNodes();

			return 0;
		}

		protected override HttpListener createListener()
		{
			return Messaging.createListener(port, false, url);
		}

		protected override void onStart()
		{
			chainNodeManager.start();

			Log.info("directory service up and running (port {0})", port);
		}

		protected override void onRequest(HttpListenerContext context)
		{
			HttpListenerResponse response = context.Response;

			if (context.Request.HttpMethod != "GET")
			{
				response.StatusCode = Messaging.HTTP_METHOD_NOT_ALLOWED;
				response.Close();
				return;
			}

			Log.info("handling incoming chain request from {0}", context.Request.RemoteEndPoint);

			StringBuilder responseData = new StringBuilder();

			var chain = chainNodeManager.getRandomChain();
			if (chain == null)
			{
				response.StatusCode = Messaging.HTTP_SERVICE_UNAVAILABLE;
				responseData.AppendLine("Not enough chain nodes available!");
			}
            else 
            {
                Log.info("Chain consists of:");
				foreach (var chainNode in chain)
				{
                    Log.info("ID: {0}, IP: {1}, region: {2}, url: {3}", chainNode.InstanceId, chainNode.IP, chainNode.Region, chainNode.Url);
					responseData.AppendLine(chainNode.Url + "/route");
					responseData.AppendLine(chainNode.PublicKeyXml);
				}
            }

			byte[] buffer = Encoding.UTF8.GetBytes(responseData.ToString());

			response.ContentLength64 = buffer.Length;
			response.OutputStream.Write(buffer, 0, buffer.Length);
			response.Close();
		}

		protected override void onStop()
		{
			chainNodeManager.stop();
			chainNodeManager = null;
		}

        static void Main(string[] args)
        {
			int port = defaultPort;
			if (args.Length >= 1)
			{
				bool success = int.TryParse(args[0], out port);
				if (!success)
					port = defaultPort;
			}
			DirectoryService directoryService = new DirectoryService(port);
            directoryService.discoverChainNodes();
			directoryService.start();
			directoryService.wait();
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
    }
}
