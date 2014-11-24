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

            Log.info("generating public/private key");
            _rsaKeys = Crypto.generateKey();

            HttpListener listener = Messaging.createListener(_port, "status", "key", "route");
            Log.info("chain node up and running (port {0})", _port);

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
            bool success = true;

            if (context.Request.Url.AbsolutePath == "/status")
            {
                Log.info("handling incoming status request from {0}", context.Request.RemoteEndPoint);
                buffer = Encoding.UTF8.GetBytes("online");
            }

            else if (context.Request.Url.AbsolutePath == "/key")
            {
                Log.info("handling incoming key request from {0}", context.Request.RemoteEndPoint);
                buffer = Encoding.UTF8.GetBytes(_rsaKeys.PublicKeyXML);
            }

            else // route request
            {
                try {
                    using (BinaryReader br = new BinaryReader(request.InputStream))
                    {
                        byte[] encryptedMessage = br.ReadBytes((int)request.ContentLength64);
                        string nextHopUrl;
                        byte[] messageForNextHop;
                        RSAParameters originPublicKey;
                        Messaging.unpackRequest(encryptedMessage, _rsaKeys.PrivateKey, out nextHopUrl, out originPublicKey, out messageForNextHop);

                        string nextHopEP = nextHopUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)[1];
                        Log.info("routing request from {0} to {1}", context.Request.RemoteEndPoint, nextHopEP);

                        if (messageForNextHop.Length == 0)
                            buffer = Messaging.sendRecv(nextHopUrl, out success);
                        else
                            buffer = Messaging.sendRecv(nextHopUrl, messageForNextHop, out success);

                        Log.info("routing response from {0} to {1}", nextHopEP, context.Request.RemoteEndPoint);

                        buffer = Crypto.encrypt(buffer, originPublicKey);
                    }
                }
                catch
                {
                    success = false;
                }

                if (!success)
                    Log.error("error while handling route request");
            }

            if (success)
            {
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
        }
    }
}
