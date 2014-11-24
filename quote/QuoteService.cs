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
        static string[] _quotes = File.ReadAllLines("quotes.txt");
        static Random  _rng = new Random();
        static int     _port = 11000;
        
        static void Main(string[] args)
        {
            HttpListener listener = Messaging.createListener(_port, "quote");            
            Log.info("quote service up and running (port {0})", _port);
           
            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerResponse response = context.Response;

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
