using System;
using System.Net;
using System.Threading;

namespace OnionRouting
{
	public abstract class OnionService
	{
		protected int port;
		protected bool running = false;

		private HttpListener listener = null;
		private Thread serviceThread = null;

		public OnionService(int port)
		{
			this.port = port;
		}

		public void start()
		{
			if (running)
				return;

			listener = createListener();
			onStart();
			running = true;
			serviceThread = new Thread(run);
			serviceThread.Start();
		}

		public void stop()
		{
			if (!running)
				return;

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

		public bool isRunning()
		{
			return running;
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
			while (running)
			{
				onRequest(listener.GetContext());
			}
		}
	}
}

