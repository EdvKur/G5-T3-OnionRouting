using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OnionRouting
{
    public class ChainNodeInfo
    {
        public string InstanceId;
        public string IP;
        public string DNS;
        public string Region;
		public int port = 8000;
        public int usageCount;

        public String Url;
        private RSAParameters publicKey;
        private String publicKeyXml;


        public RSAParameters PublicKey
        {
            get { return publicKey; }
            set
            {
                publicKey = value;
                publicKeyXml = Crypto.exportKey(value);
            }
        }
        public String PublicKeyXml
        {
            get { return publicKeyXml; }
            set
            {
                publicKeyXml = value;
                publicKey = Crypto.importKey(value);
            }
        }
                
        public ChainNodeInfo(string name, string ip, string dns, string region)
        {
            InstanceId = name;
            IP = ip;
            DNS = dns;
            Region = region;
            usageCount = 0;
        }

        public ChainNodeInfo()
        {

        }
    }
}
