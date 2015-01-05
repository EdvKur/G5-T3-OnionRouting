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
	public class ChainService : OnionService
    {
		const int DEFAULT_PORT = 8000;

		private RSAKeyPair _rsaKeys;

		public ChainService(int port = DEFAULT_PORT)
			: base(port)
		{
		}

		protected override HttpListener createListener()
		{
			return Messaging.createListener(port, false, "status", "key", "route");
		}

		protected override void onStart()
		{
			Log.info("generating public/private key");
			_rsaKeys = Crypto.generateKey();
			Log.info("chain node up and running (port {0})", port);
		}

		protected override void onRequest(HttpListenerContext context)
		{
			new ChainRequestHandler(_rsaKeys, context).start();
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
			ChainService chainService = new ChainService(port);
			chainService.start();
			chainService.wait();
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
