using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OnionRouting
{
    public class RSAKeyPair
    {
        public readonly RSAParameters PrivateKey, PublicKey;
        public readonly String PrivateKeyXML, PublicKeyXML;

        public RSAKeyPair(RSAParameters privateKey, RSAParameters publicKey, String privateKeyXML, String publicKeyXML)
        {
            PrivateKey = privateKey;
            PublicKey = publicKey;
            PrivateKeyXML = privateKeyXML;
            PublicKeyXML = publicKeyXML;
        }
    }
}
