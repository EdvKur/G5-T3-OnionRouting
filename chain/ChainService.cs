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
		static int defaultPort;

        string statusUrl;
        string keyUrl;
        string routeUrl;

		private RSAKeyPair _rsaKeys;

        static ChainService()
        {
            defaultPort = Properties.Settings.Default.defaultPort;
        }

		public ChainService(int port)
			: base(port)
		{
            statusUrl = Properties.Settings.Default.statusUrl;
            keyUrl = Properties.Settings.Default.keyUrl;
            routeUrl = Properties.Settings.Default.routeUrl;
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
			int port = defaultPort;
			if (args.Length >= 1)
			{
				bool success = int.TryParse(args[0], out port);
				if (!success)
					port = defaultPort;
			}
			ChainService chainService = new ChainService(port);
			chainService.start();
			chainService.wait();
        }             
    }
}
