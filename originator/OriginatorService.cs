using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OnionRouting
{
    class OriginatorService
    {
        static int _port;
        static string _directoryServiceUrl;
        static string _quoteServiceUrl;


        static void init()
        {

        }

        static void Main(string[] args)
        {
            parseArgs(args);

            Console.WriteLine("originator up and running (port {0})", _port);
            Console.WriteLine();
            
            while (true)
            {
                Console.Write("Press enter to request a quote... ");
                Console.ReadLine();

                var chain = requestChain();
                byte[] requestData = Messaging.buildRequest(_quoteServiceUrl, chain);

                Console.WriteLine(Encoding.UTF8.GetString(Messaging.sendRecv(chain[0].Url, requestData)));
                Console.WriteLine();
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
            byte[] responceData = Messaging.sendRecv(_directoryServiceUrl);

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
