﻿using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Security.Cryptography;

namespace OnionRouting
{
	public class ChainRequestHandler
	{
		private const string ONLINE = "online";

        string statusUrl;
        string keyUrl;

		private RSAKeyPair rsaKeys;
		private HttpListenerContext context;
		private bool started = false;

		public ChainRequestHandler(RSAKeyPair rsaKeys, HttpListenerContext context)
		{
			this.rsaKeys = rsaKeys;
			this.context = context;

            statusUrl = Properties.Settings.Default.statusUrl;
            keyUrl = Properties.Settings.Default.keyUrl;
		}

		public void start()
		{
			if (started)
				return;

			started = true;
			new Thread(run).Start();
		}

		private void run()
		{
			if (context.Request.Url.AbsolutePath == "/" + statusUrl)
				handleStatusRequest();
			else if (context.Request.Url.AbsolutePath == "/" + keyUrl)
				handleKeyRequest();
			else
				handleRouteRequest();
		}

		private void handleStatusRequest()
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
			response.ContentLength64 = ONLINE.Length;

			StreamWriter writer = new StreamWriter(response.OutputStream);
			writer.Write(ONLINE);
			writer.Close();
			response.Close();
		}

		private void handleKeyRequest()
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
			response.ContentLength64 = rsaKeys.PublicKeyXML.Length;

			StreamWriter writer = new StreamWriter(response.OutputStream);
			writer.Write(rsaKeys.PublicKeyXML);
			writer.Close();
			response.Close();
		}

		private void handleRouteRequest()
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
					Messaging.unpackRequest(encryptedMessage, rsaKeys.PrivateKey, out nextHopUrl, out originPublicKey, out messageForNextHop);

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
					response.StatusCode = Messaging.HTTP_OK;
					response.OutputStream.Write(buffer, 0, buffer.Length);
				}
			}
			catch (Exception e)
			{
				Log.error("error while handling route request: " + e.ToString());
				response.StatusCode = Messaging.HTTP_SERVER_ERROR;
			}
			finally {
				response.Close();
			}
		}
	}
}
