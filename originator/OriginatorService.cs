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
        static RSAKeyPair _rsaKeys;
                
        static void Main(string[] args)
        {           
            parseArgs(args);
                        
            Log.info("originator up and running (port {0})", _port);
            new Thread(handleRequests).Start();
            
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Press enter to request a quote... ");
                Console.WriteLine();
                Console.ReadKey(true);

                bool success;
                requestQuote(out success);
            }
        }

        private static string requestQuote(out bool success, bool returnMetainfos = false)
        {
            Log.info("quote requested");
            _rsaKeys = Crypto.generateKey();

            int retry = 1;
            while (retry <= 5)
            {
                var chain = requestChain();
                if (chain == null)
                    Log.error("failed to retrieve valid chain ({0}/5 attempts)", retry);

                else
                {
                    byte[] requestData = Messaging.buildRequest(_quoteServiceUrl, chain, _rsaKeys.PublicKey);
                    byte[] responseData = Messaging.sendRecv(chain[0].Url, requestData, 1500, out success);

                    if (success)
                    {
                        for (int i = 0; i < chain.Count; i++)
                            responseData = Crypto.decrypt(responseData, _rsaKeys.PrivateKey);
                        
                        string quote = Encoding.UTF8.GetString(responseData);
                        Log.info("quote received: {0}", quote);

                        if (returnMetainfos) return prepareMetaInfos(quote, chain);
                        else return null;
                    }
                    else
                        Log.error("error routing request ({0}/5 attempts)", retry);
                }
                retry++;
            }

            if (retry > 5)
                Log.error("request aborted");

            success = false;
            return null;
        }

        private static string prepareMetaInfos(string quote, List<ChainNodeData> chain)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(quote);

            foreach (var item in chain)
                sb.AppendLine(item.Url.Split(new[]{'/'}, StringSplitOptions.RemoveEmptyEntries)[1]);

            sb.AppendLine(_rsaKeys.PublicKeyXML);
            foreach (var item in chain)
                sb.AppendLine(item.PublicKeyXml);

            return sb.ToString();
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
                    bool success = true;
                    string metaInfos = requestQuote(out success, true);

                    if (success)
                        buffer = Encoding.UTF8.GetBytes(metaInfos);           

                    else
                    {
                        buffer = new byte[0];
                        response.StatusCode = 500;
                    }
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
                chain.Add(new ChainNodeData(lines[i], Crypto.importKey(lines[i + 1])));
            }

            return chain;
        }
    }
}
