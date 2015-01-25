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
	public class OriginatorService : OnionService
    {
		static int defaultPort;        
		static string defaultDirectoryServiceUrl;
		static string defaultQuoteServiceUrl;
        static string handleUrl;
        static string uiUrl;

		private string directoryServiceUrl;
		private string quoteServiceUrl;

        static OriginatorService()
        {
            defaultPort = Properties.Settings.Default.defaultPort;
			defaultDirectoryServiceUrl = Properties.Settings.Default.directoryServiceUrl;
			defaultQuoteServiceUrl = Properties.Settings.Default.quoteServiceUrl;
            handleUrl = Properties.Settings.Default.handleUrl;
            uiUrl = Properties.Settings.Default.uiUrl;
        }
	public OriginatorService(int port)
		: base(port)
	{
		directoryServiceUrl = defaultDirectoryServiceUrl;
		quoteServiceUrl = defaultQuoteServiceUrl;
	}

	public OriginatorService(int port, string directoryUrl, string quoteUrl)
		: base(port)
	{
		directoryServiceUrl = directoryUrl;
		quoteServiceUrl = quoteUrl;
	}

		public List<ChainNodeInfo> requestChain()
		{
			bool success;
			byte[] responceData = Messaging.sendRecv(directoryServiceUrl, out success);

			if (!success) return null;

			string[] lines = Encoding.UTF8.GetString(responceData)
				.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

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

		public string requestQuote(out bool success, out string quote, bool returnMetainfos = false)
		{
			Log.info("quote requested");
			quote = null;
			RSAKeyPair _rsaKeys = Crypto.generateKey();

			int retry = 1;
			while (retry <= 5)
			{
				var chain = requestChain();
				if (chain == null)
					Log.error("failed to retrieve valid chain ({0}/5 attempts)", retry);

				else
				{
					byte[] requestData = Messaging.buildRequest(quoteServiceUrl, chain, _rsaKeys.PublicKey);
					byte[] responseData = Messaging.sendRecv(chain[0].Url, requestData, 1500, out success);

					if (success)
					{
						for (int i = 0; i < chain.Count; i++)
							responseData = Crypto.decrypt(responseData, _rsaKeys.PrivateKey);

						quote = Encoding.UTF8.GetString(responseData);
						Log.info("quote received: {0}", quote);

						if (returnMetainfos) return prepareMetaInfos(quote, chain, _rsaKeys);
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

		protected override HttpListener createListener()
		{
			return Messaging.createListener(port, true, handleUrl, uiUrl);
		}

		protected override void onStart()
		{
			Log.info("originator up and running (port {0})", port);
		}

		protected override void onRequest(HttpListenerContext context)
		{
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;

			if (context.Request.HttpMethod != "GET")
			{
				response.StatusCode = Messaging.HTTP_METHOD_NOT_ALLOWED;
				response.Close();
				return;
			}

			byte[] buffer = null;

			if (context.Request.Url.AbsolutePath == "/" + uiUrl)
			{
				buffer = File.ReadAllBytes("web.html");
				response.ContentType = "text/html";
			}

			else if (context.Request.Url.AbsolutePath == "/" + handleUrl)
			{
				bool success = true;
				string quote;
				string metaInfos = requestQuote(out success, out quote, true);

				if (success)
					buffer = Encoding.UTF8.GetBytes(metaInfos);

				else
				{
					buffer = new byte[0];
					response.StatusCode = Messaging.HTTP_SERVER_ERROR;
				}
			}

			response.ContentLength64 = buffer.Length;
			response.OutputStream.Write(buffer, 0, buffer.Length);
			response.Close();
		}

		protected override void onStop()
		{
		}

		private string prepareMetaInfos(string quote, List<ChainNodeInfo> chain, RSAKeyPair _rsaKeys)
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

		static void Main(string[] args)
		{
			int port = defaultPort;
			if (args.Length >= 1)
			{
				bool success = int.TryParse(args[0], out port);
				if (!success)
					port = defaultPort;
			}

			OriginatorService originatorService = new OriginatorService(port);
			originatorService.start();

			while (true)
			{
				Console.WriteLine();
				Console.WriteLine("Press enter to request a quote... ");
				Console.WriteLine();
				Console.ReadKey(true);

				bool success;
				string quote;
				originatorService.requestQuote(out success, out quote);
			}

//			originatorService.stop();
		}
    }
}
