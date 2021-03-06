﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OnionRouting
{
	public class QuoteService : OnionService
    {
		static int defaultPort;
        static string quoteFileLocation;
        static string url;

		private string[] _quotes;
		private Random   _rng    = new Random();

        static QuoteService()
        {
            defaultPort = Properties.Settings.Default.defaultPort;
            quoteFileLocation = Properties.Settings.Default.quoteFileLocation;
            url = Properties.Settings.Default.url;
        }

		public QuoteService(int port, string[] quotes = null)
			: base(port)
		{
			if (quotes == null)
                try
                {
                    quotes = File.ReadAllLines(quoteFileLocation);
                }
                catch (FileNotFoundException e)
                {
                    Log.error("Quotefile was not found under the specified path: {0}", quoteFileLocation);
                    Console.WriteLine("Press enter to exit...");
                    Console.ReadLine();
                    System.Environment.Exit(1);
                }

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
			return Messaging.createListener(port, false, url);
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

            Log.info("handling incoming quote request from {0}, responded '{1}'", context.Request.RemoteEndPoint, quote);

			response.Close();
		}

		private string getRandomQuote()
        {
            return _quotes[_rng.Next(_quotes.Length)];
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
			QuoteService quoteService = new QuoteService(port);
			quoteService.start();
			quoteService.wait();
		}
    }
}
