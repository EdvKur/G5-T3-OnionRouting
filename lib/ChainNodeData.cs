﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OnionRouting
{
    class ChainNodeData2
    {
        public readonly String Url;
        public readonly RSAParameters PublicKey;
        public readonly String PublicKeyXml;

        public ChainNodeData2(String url, RSAParameters publicKey)
        {
            Url = url;
            PublicKey = publicKey;
            PublicKeyXml = Crypto.exportKey(publicKey);
        }

        public ChainNodeData2(String url, String publicKeyXml)
        {        
            Url = url;
            PublicKey = Crypto.importKey(publicKeyXml); ;
            PublicKeyXml = publicKeyXml;
        }
    }
}
