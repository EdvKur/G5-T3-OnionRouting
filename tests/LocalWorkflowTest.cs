using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OnionRouting
{
	/// <summary>
	/// Tries to test as much as is automatically and locally testable. This involves starting all the neccessary services locally and
	/// routing a request through them.
	/// 
	/// This does NOT test:
	/// - chain node load balancing
	/// - automatically starting nodes
	/// - interaction with EC2
	/// - originator UI
	/// </summary>
	[TestFixture]
	public class LocalWorkflowTest
	{
		const int CHAIN_NODES_COUNT = 3;
		const int START_PORT = 9100;
		const string TEST_QUOTE = "asjdnakjsdnkjasndkn aksjd nakjsdn akjsd nka sdks";

		private QuoteService quoteService = null;
		private ChainNodeInfo[] chainNodeInfos = null;
		private ChainService[] chainServices = null;
		private DirectoryService directoryService = null;
		private OriginatorService originatorService = null;

		[SetUp]
		protected void setUp()
		{
			Log.info("setting up");
			int currentPort = START_PORT;

			// Quote Service
			quoteService = new QuoteService(++currentPort, new string[1] { TEST_QUOTE });
			quoteService.start();

			// Chain Services
			chainNodeInfos = new ChainNodeInfo[CHAIN_NODES_COUNT];
			chainServices = new ChainService[CHAIN_NODES_COUNT];
			for (int i = 0; i < CHAIN_NODES_COUNT; i++)
			{
				int nodePort = ++currentPort;
				chainNodeInfos[i] = new ChainNodeInfo("TestNode-" + i, "localhost", "", "");
				chainNodeInfos[i].port = nodePort;
				chainNodeInfos[i].Url = "http://localhost:" + nodePort;

				chainServices[i] = new ChainService(nodePort);
				chainServices[i].start();
			}

			// Directory Service
			directoryService = new DirectoryService(++currentPort);
			directoryService.AutoStartChainNodes = false;
			directoryService.start();
			foreach (ChainNodeInfo nodeInfo in chainNodeInfos)
			{
				directoryService.addManualChainNode(nodeInfo);
			}

			// Originator Service
			string directoryUrl = "http://localhost:" + directoryService.getPort() + "/chain";
			string quoteUrl = "http://localhost:" + quoteService.getPort() + "/quote";
			originatorService = new OriginatorService(++currentPort, directoryUrl, quoteUrl);
			originatorService.start();
		}

		[TearDown]
		protected void tearDown()
		{
			directoryService.stop();
			directoryService = null;

			foreach (ChainService service in chainServices)
			{
				service.stop();
			}
			chainNodeInfos = null;
			chainServices = null;

			quoteService.stop();
			quoteService = null;

			originatorService.stop();
			originatorService = null;
		}

		[Test]
		public void testWorkflow()
		{
			bool allReady = false;
			while (!allReady)
			{
				allReady = true;
				if (!quoteService.isReady())
					allReady = false;
				foreach (ChainService chainService in chainServices)
				{
					if (!chainService.isReady())
						allReady = false;
				}

				if (!directoryService.isReady() || (directoryService.countReadyNodes() < 3))
					allReady = false;

				if (!originatorService.isReady())
					allReady = false;

				Thread.Sleep(100);
			}

			List<ChainNodeInfo> chain = originatorService.requestChain();
			Assert.IsTrue(chain != null);
			Assert.AreEqual(3, chain.Count);

			bool quoteSuccess;
			string quote;
			originatorService.requestQuote(out quoteSuccess, out quote);
			Assert.IsTrue(quoteSuccess);
			Assert.AreEqual(TEST_QUOTE, quote);
		}
	}
}
