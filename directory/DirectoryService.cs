using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;


namespace OnionRouting
{
    class DirectoryService
    {
        const int PORT = 8000;

        static void Main(string[] args)
        {
			ChainNodeManager chainNodeManager = new ChainNodeManager();
			chainNodeManager.AutoStartChainNodes = true;
			chainNodeManager.start();

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

				var chain = chainNodeManager.getRandomChain();
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

//			chainNodeManager.stop();
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
