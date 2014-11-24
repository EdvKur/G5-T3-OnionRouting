using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OnionRouting
{
    class OriginatorService
    {
        static int _port;
        static string _directoryServiceUrl;
        static string _quoteServiceUrl;
                
        static void Main(string[] args)
        {
            parseArgs(args);

            Console.WriteLine("originator up and running (port {0})", _port);
            Console.WriteLine();

            new Thread(handleRequests).Start();
            
            while (true)
            {
                Console.Write("Press enter to request a quote... ");
                Console.ReadLine();

                var chain = requestChain();
                if (chain == null)
                {
                    Console.WriteLine("failed to retrieve chain, directory node currently not available");
                }
                else
                {
                    byte[] requestData = Messaging.buildRequest(_quoteServiceUrl, chain);

                    bool success;
                    byte[] responseData = Messaging.sendRecv(chain[0].Url, requestData, 1500, out success);
                    if (success)
                        Console.WriteLine(Encoding.UTF8.GetString(responseData));
                    else
                        Console.WriteLine("error routing request... retrying (TODO)");

                }
                Console.WriteLine();
            }
        }

        private static void handleRequests()
        {
            HttpListener listener = Messaging.createListener(_port, "handle", "ui");
            while (true)
            {
                var context = listener.GetContext();                
                
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                byte[] buffer = null;

                if (context.Request.Url.AbsolutePath == "/ui")
                {
                    buffer = File.ReadAllBytes("web.html");
                    response.ContentType = "application/xhtml+xml";
                }

                else if (context.Request.Url.AbsolutePath == "/handle")
                {
                    buffer = Encoding.UTF8.GetBytes(
                        "The reason the way of the sinner is hard is because it is so crowded.\r\n" +
                        "IP von Entry Node\r\n" +
                        "IP von Intermediary Node\r\n" +
                        "IP von Exit Node\r\n" +
                        "Nachricht von Origin nach Entry\r\n" +
                        "Nachricht von Entry nach Intermediary\r\n" +
                        "Nachricht von Intermediary nach Exit\r\n" +
                        "Nachricht von Exit nach Ouote\r\n" +
                        "Nachricht von Ouote nach Exit\r\n" +
                        "Nachricht von Exit nach Intermediary\r\n" +
                        "Nachricht von Intermediary nach Entry\r\n" +
                        "Nachricht von Entry nach Origin\r\n"
                    );                             
                }

                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
        }

        private static void parseArgs(string[] args)
        {
            _port = 7000;
            _directoryServiceUrl = "http://localhost:8000/chain";
            _quoteServiceUrl     = "http://localhost:11000/quote";
            
            // TODO
            // Environment.Exit(1);
        }
              

        static List<ChainNodeData> requestChain()
        {
            bool success;
            byte[] responceData = Messaging.sendRecv(_directoryServiceUrl, out success);

            if (!success) return null;

            string[] lines = Encoding.UTF8.GetString(responceData)
                            .Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length <= 1 || lines.Length % 2 == 1)
            {
                // no valid chain received
                return null;
            }

            List<ChainNodeData> chain = new List<ChainNodeData>();

            for (int i = 0; i < lines.Length; i += 2)
            {
                // TODO error handling (e.g. check if responce is valid)
                chain.Add(new ChainNodeData(lines[i], Crypto.parseKey(lines[i + 1])));
            }

            return chain;
        }
    }
}
