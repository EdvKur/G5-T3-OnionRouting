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
        static int _port = 8000;
        static int _chainLength = 3;
        static int _targetChainNodeCount = 6;
        static int _aliveCheckInterval   = 10000;
        static int _aliveCheckTimeout    = 3000;

        static List<ChainNodeData> _chainNodes = new List<ChainNodeData>();      // <IP:Port, PublicKey in Base64>
        static Random _rng = new Random();

        static void Main(string[] args)
        {
            discoverChainNodes();

            new Thread(aliveCheck).Start();
            HttpListener listener = Messaging.createListener(_port, "chain");

            Console.WriteLine("directory node up and running (port {0})", _port);
            Console.WriteLine();


            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerResponse response = context.Response;

                Console.WriteLine("chain request received");

                StringBuilder responseData = new StringBuilder();

                var chain = getRandomChain();
                if (chain == null)
                {
                    response.StatusCode = 503;      // service unavailable
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
                response.OutputStream.Close();
            }
        }

        static void discoverChainNodes()
        {
            Console.WriteLine("Discovering chain nodes");

            for (int i = 9000; i < 9006; i++)
            {
                bool success;
                string url = "http://localhost:" + i;

                byte[] response = Messaging.sendRecv(url + "/key", out success);

                if (success)
                {
                    string xml = Encoding.UTF8.GetString(response);
                    _chainNodes.Add(new ChainNodeData(url, xml));
                    
                    Console.WriteLine("chain node at " + url + " discovered");
                }
                else
                    Console.WriteLine("chain node at " + url + " not available");
            }
        }
        
        static IEnumerable<ChainNodeData> getRandomChain()
        {
            //return _chainNodes; // TODO remove this line

            lock (_chainNodes)
            {
                if (_chainNodes.Count < _chainLength)
                    return null;

                return _chainNodes.OrderBy(x => _rng.Next()).Take(_chainLength);
            }
        }


        static void aliveCheck()
        {
            while (true)
            {
                Task<WebResponse>[] tasks = new Task<WebResponse>[_chainNodes.Count];
                
                for (int i = 0; i < _chainNodes.Count; i++)
                    tasks[i] = HttpWebRequest.CreateHttp(_chainNodes[i].Url + "/status").GetResponseAsync();
                
                Thread.Sleep(_aliveCheckTimeout);
                
                int j = 0;
                for (int i = 0; i < tasks.Length; i++)
                {
                    if (!tasks[i].IsCompleted)
                    {
                        Console.WriteLine("Chain node at {0} gone offline!", _chainNodes[j].Url);
                        lock (_chainNodes)
                        {
                            _chainNodes.RemoveAt(j);
                            j--;
                        }
                    }
                    j++;
                }

                Console.WriteLine("chain node status checked");
                Thread.Sleep(_aliveCheckInterval - _aliveCheckTimeout);
            }
        }

       
    }
}
