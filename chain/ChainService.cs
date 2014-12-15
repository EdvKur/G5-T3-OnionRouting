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
        const int PORT = 8000;

		private static RSAKeyPair _rsaKeys;

        static void Main(string[] args)
        {
            Log.info("generating public/private key");
            _rsaKeys = Crypto.generateKey();

            HttpListener listener = Messaging.createListener(PORT, false, "status", "key", "route");
            Log.info("chain node up and running (port {0})", PORT);

            while (true)
            {
                new Thread(handleRequest).Start(listener.GetContext());
            }
        }
        
        static void handleRequest(Object obj)
        {
            HttpListenerContext context = (HttpListenerContext)obj;

            if (context.Request.Url.AbsolutePath == "/status")
            {
				handleStatusRequest(context);
            }
            else if (context.Request.Url.AbsolutePath == "/key")
            {
				handleKeyRequest(context);
            }
            else
            {
				handleRouteRequest(context);
            }
        }

		private static void handleStatusRequest(HttpListenerContext context)
		{
			Log.info("handling incoming status request from {0}", context.Request.RemoteEndPoint);
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;

			if (request.HttpMethod != "GET")
			{
				response.StatusCode = Messaging.HTTP_METHOD_NOT_ALLOWED;
				response.Close();
				return;
			}
			response.StatusCode = Messaging.HTTP_OK;

			StreamWriter writer = new StreamWriter(response.OutputStream);
			writer.Write("online");
			writer.Close();
			response.Close();
		}

		private static void handleKeyRequest(HttpListenerContext context)
		{
			Log.info("handling incoming key request from {0}", context.Request.RemoteEndPoint);
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;

			if (request.HttpMethod != "GET")
			{
				response.StatusCode = Messaging.HTTP_METHOD_NOT_ALLOWED;
				response.Close();
				return;
			}
			response.StatusCode = Messaging.HTTP_OK;

			StreamWriter writer = new StreamWriter(response.OutputStream);
			writer.Write(_rsaKeys.PublicKeyXML);
			writer.Close();
			response.Close();
		}

		private static void handleRouteRequest(HttpListenerContext context)
		{
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;

			if (request.HttpMethod != "POST")
			{
				response.StatusCode = Messaging.HTTP_METHOD_NOT_ALLOWED;
				response.Close();
				return;
			}

			byte[] buffer = null;

			try {
				using (BinaryReader br = new BinaryReader(request.InputStream))
				{
					byte[] encryptedMessage = br.ReadBytes((int)request.ContentLength64);
					string nextHopUrl;
					byte[] messageForNextHop;
					bool success = true;
					RSAParameters originPublicKey;
					Messaging.unpackRequest(encryptedMessage, _rsaKeys.PrivateKey, out nextHopUrl, out originPublicKey, out messageForNextHop);

					string nextHopEP = nextHopUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)[1];
					Log.info("routing request from {0} to {1}", context.Request.RemoteEndPoint, nextHopEP);

					if (messageForNextHop.Length == 0)
						buffer = Messaging.sendRecv(nextHopUrl, out success);
					else
						buffer = Messaging.sendRecv(nextHopUrl, messageForNextHop, out success);

					if (!success) throw new Exception();

					Log.info("routing response from {0} to {1}", nextHopEP, context.Request.RemoteEndPoint);

					buffer = Crypto.encrypt(buffer, originPublicKey);

					response.ContentLength64 = buffer.Length;
					response.OutputStream.Write(buffer, 0, buffer.Length);
					response.StatusCode = Messaging.HTTP_OK;
				}
			}
			catch
			{
				Log.error("error while handling route request");
				response.StatusCode = Messaging.HTTP_SERVER_ERROR;
			}
			finally {
				response.Close();
			}
		}

        //private static void parseArgs(string[] args)
        //{
        //    if (args.Length == 0 || !int.TryParse(args[0], out PORT) || PORT <= 0 || PORT > 65535)
        //    {
        //        Console.WriteLine("Error: valid port required!");
        //        Console.WriteLine("Usage: chain port");
        //        Environment.Exit(1);
        //    }
        //}              
    }
}
