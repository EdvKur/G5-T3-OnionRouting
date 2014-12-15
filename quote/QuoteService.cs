using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OnionRouting
{
    class QuoteService
    {
        const int PORT = 8000;

        static string[] _quotes = File.ReadAllLines("quotes.txt");
        static Random   _rng    = new Random();
                
        static void Main(string[] args)
        {
            HttpListener listener = Messaging.createListener(PORT, false, "quote");
            Log.info("quote service up and running (port {0})", PORT);
           
            while (true)
            {
                HttpListenerContext context = listener.GetContext();
				HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

				if (request.HttpMethod != "GET") {
					response.StatusCode = Messaging.HTTP_METHOD_NOT_ALLOWED;
					response.Close();
					continue;
				}

				response.StatusCode = Messaging.HTTP_OK;

                string quote = getRandomQuote();
                byte[] buffer = Encoding.UTF8.GetBytes(quote);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                Log.info("handling incoming quote request from {0}", context.Request.RemoteEndPoint);
            }
        }

        static string getRandomQuote()
        {
            return _quotes[_rng.Next(_quotes.Length)];
        }
    }
}
