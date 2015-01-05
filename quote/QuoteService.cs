using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OnionRouting
{
	public class QuoteService : OnionService
    {
		const int DEFAULT_PORT = 8000;

		private string[] _quotes;
		private Random   _rng    = new Random();

		public QuoteService(int port = DEFAULT_PORT, string[] quotes = null)
			: base(port)
		{
			if (quotes == null)
				quotes = File.ReadAllLines("quotes.txt");

			List<string> nonEmptyLines = new List<string>();
			foreach (string line in quotes)
			{
				if (!string.IsNullOrWhiteSpace(line))
					nonEmptyLines.Add(line);
			}

			_quotes = nonEmptyLines.ToArray();
		}

		protected override HttpListener createListener()
		{
			return Messaging.createListener(port, false, "quote");
		}

		protected override void onStart()
		{
			Log.info("quote service up and running (port {0})", port);
		}

		protected override void onRequest(HttpListenerContext context)
		{
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;

			if (request.HttpMethod != "GET") {
				response.StatusCode = Messaging.HTTP_METHOD_NOT_ALLOWED;
				response.Close();
				return;
			}

			response.StatusCode = Messaging.HTTP_OK;

			string quote = getRandomQuote();
			byte[] buffer = Encoding.UTF8.GetBytes(quote);
			response.ContentLength64 = buffer.Length;
			response.OutputStream.Write(buffer, 0, buffer.Length);
			response.Close();

			Log.info("handling incoming quote request from {0}", context.Request.RemoteEndPoint);
		}

		private string getRandomQuote()
        {
            return _quotes[_rng.Next(_quotes.Length)];
        }

		static void Main(string[] args)
		{
			int port = DEFAULT_PORT;
			if (args.Length >= 1)
			{
				bool success = int.TryParse(args[0], out port);
				if (!success)
					port = DEFAULT_PORT;
			}
			QuoteService quoteService = new QuoteService(port);
			quoteService.start();
			quoteService.wait();
		}
    }
}
