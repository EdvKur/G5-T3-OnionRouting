using System;
using System.Net;
using System.Threading;

namespace OnionRouting
{
	public abstract class OnionService
	{
		protected int port;
		protected bool running = false;
		private bool ready = false;

		private HttpListener listener = null;
		private Thread serviceThread = null;

		public OnionService(int port)
		{
			this.port = port;
		}

		public int getPort()
		{
			return port;
		}

		public void start()
		{
			if (running)
				return;

			listener = createListener();
			running = true;
			serviceThread = new Thread(run);
			serviceThread.Start();
		}

		public void stop()
		{
			if (!running)
				return;

			ready = false;
			running = false;
			onStop();

			if (listener != null)
			{
				listener.Close();
				listener = null;
			}

			if (serviceThread != null)
			{
				serviceThread.Join();
				serviceThread = null;
			}
		}

		public bool isReady()
		{
			return ready;
		}

		public void wait()
		{
			if (serviceThread != null)
				serviceThread.Join();
		}

		abstract protected HttpListener createListener();

		protected virtual void onStart() {}

		protected virtual void onStop() {}

		abstract protected void onRequest(HttpListenerContext context);

		private void run()
		{
			onStart();
			ready = true;
			while (running)
			{
				try
				{
					onRequest(listener.GetContext());
				}
				catch (HttpListenerException e)
				{
					if (listener == null || !listener.IsListening)
					{
						// the listener has been closed, no point in continuing
						break;
					}
					else
					{
						Log.error(e.ToString());
					}
				}
			}
		}
	}
}

