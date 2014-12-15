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
				//new Thread(handleRequest).Start(listener.GetContext());
				new ChainRequestHandler(_rsaKeys, listener.GetContext()).start();
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
