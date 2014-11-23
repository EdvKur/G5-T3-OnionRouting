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
    class ChainService
    {
        static int _port = 9000;
        static RSAKeyPair _rsaKeys;

        static void Main(string[] args)
        {
            parseArgs(args);

            Console.Write("generating public/private key... ");
            _rsaKeys = Crypto.generateKey();
            Console.WriteLine("done!");

            HttpListener listener = Messaging.createListener(_port, "status", "key", "route");
            Console.WriteLine("chain node up and running (port {0})", _port);

            while (true)
            {
                new Thread(handleRequest).Start(listener.GetContext());
            }
        }

        private static void parseArgs(string[] args)
        {
            if (args.Length == 0 || !int.TryParse(args[0], out _port) || _port <= 0 || _port > 65535)
            {
                Console.WriteLine("Error: valid port required!");
                Console.WriteLine("Usage: chain port");
                Environment.Exit(1);
            }
        }
              

        static void handleRequest(Object obj)
        {
            HttpListenerContext context = (HttpListenerContext)obj;
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            byte[] buffer = null;

            if (context.Request.Url.AbsolutePath == "/status")
            {
                Console.WriteLine("status request received");
                buffer = Encoding.UTF8.GetBytes("online");
            }

            else if (context.Request.Url.AbsolutePath == "/key")
            {
                Console.WriteLine("key request received");
                buffer = Encoding.UTF8.GetBytes(_rsaKeys.PublicKeyXML);
            }

            else // route request
            {
                Console.WriteLine("route request received");

                using (BinaryReader br = new BinaryReader(request.InputStream))
                {
                    byte[] encryptedMessage = br.ReadBytes((int)request.ContentLength64);
                    string nextHopUrl;
                    byte[] messageForNextHop;
                    Messaging.unpackRequest(encryptedMessage, _rsaKeys.PrivateKey, out nextHopUrl, out messageForNextHop);

                    if (messageForNextHop.Length == 0)
                        buffer = Messaging.sendRecv(nextHopUrl);
                    else
                        buffer = Messaging.sendRecv(nextHopUrl, messageForNextHop);
                }
            }

            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}
