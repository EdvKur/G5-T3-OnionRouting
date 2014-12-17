using System;
using NUnit.Framework;

namespace OnionRouting
{
	[TestFixture]
	public class CryptoTest
	{
		const int TEST_SIZE = 1024;

		private RSAKeyPair keyPair;

		[SetUp]
		protected void setUp()
		{
			keyPair = Crypto.generateKey();
		}

		[Test]
		public void testEncrpytion()
		{
			byte[] message = new byte[TEST_SIZE];
			Random rng = new Random();
			rng.NextBytes(message);

			byte[] encryptedMessage = Crypto.encrypt(message, keyPair.PublicKey);
			byte[] decryptedMessage = Crypto.decrypt(encryptedMessage, keyPair.PrivateKey);

			for (int i = 0; i < TEST_SIZE; ++i)
			{
				Assert.AreEqual(message[i], decryptedMessage[i]);
			}
		}
	}
}
