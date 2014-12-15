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
        const int PORT = 8000;
        const string DIRECTORY_SERVICE_URL = "http://54.93.191.79:8000/chain";
        const string QUOTE_SERVICE_URL = "http://54.93.192.53:8000/quote";
       
        static RSAKeyPair _rsaKeys;
                
        static void Main(string[] args)
        {
            new Thread(handleRequests).Start();      
            Log.info("originator up and running (port {0})", PORT);
                        
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
                    byte[] requestData = Messaging.buildRequest(QUOTE_SERVICE_URL, chain, _rsaKeys.PublicKey);
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

        private static string prepareMetaInfos(string quote, List<ChainNodeInfo> chain)
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
            HttpListener listener = Messaging.createListener(PORT, true, "handle", "ui");
            while (true)
            {
                var context = listener.GetContext();
                
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                byte[] buffer = null;

                if (context.Request.Url.AbsolutePath == "/ui")
                {
                    buffer = File.ReadAllBytes("web.html");
                    response.ContentType = "text/html";
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

        static List<ChainNodeInfo> requestChain()
        {
            bool success;
            byte[] responceData = Messaging.sendRecv(DIRECTORY_SERVICE_URL, out success);

            if (!success) return null;

            string[] lines = Encoding.UTF8.GetString(responceData)
                            .Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length <= 1 || lines.Length % 2 == 1)
            {
                // no valid chain received
                return null;
            }

            List<ChainNodeInfo> chain = new List<ChainNodeInfo>();

            for (int i = 0; i < lines.Length; i += 2)
            {
                // TODO error handling (e.g. check if responce is valid)
                chain.Add(new ChainNodeInfo() {
                        Url = lines[i],
                        PublicKey = Crypto.importKey(lines[i + 1])
                    });
            }

            return chain;
        }
    }
}
