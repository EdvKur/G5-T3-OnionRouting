using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OnionRouting
{
    public class Messaging
    {
		public const int HTTP_OK = 200;
		public const int HTTP_METHOD_NOT_ALLOWED = 405;
		public const int HTTP_SERVER_ERROR = 500;
		public const int HTTP_SERVICE_UNAVAILABLE = 503;

        public static HttpListener createListener(int port, bool localhostPrefixOnly = false, params string[] prefixes)
        {
            HttpListener listener = new HttpListener();
            foreach (var prefix in prefixes)
                if (localhostPrefixOnly)
                    listener.Prefixes.Add("http://localhost:" + port + "/" + prefix + "/");
                else
                    listener.Prefixes.Add("http://+:" + port + "/" + prefix + "/");

            listener.Start();
            return listener;
        }

        public static byte[] sendRecv(string url, out bool success)
        {
            try
            {
                HttpWebRequest req = HttpWebRequest.CreateHttp(url);
                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                var resStream = resp.GetResponseStream();
                using (BinaryReader br = new BinaryReader(resStream))
                {
                    byte[] result = br.ReadBytes((int)resp.ContentLength);
                    success = true;
                    return result;
                }
            }
			catch
			{
                success = false;
                return null;
            }
        }

		public static byte[] sendRecv(string url, byte[] data, out bool success)
        {
            try
            {
                HttpWebRequest req = HttpWebRequest.CreateHttp(url);
	            req.Method = "POST";
	            req.ContentType = "multipart/form-data";
                req.ContentLength = data.Length;

                using (var stream = req.GetRequestStream())
                    stream.Write(data, 0, data.Length);

                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                var resStream = resp.GetResponseStream();
                using (BinaryReader br = new BinaryReader(resStream))
                {
                    byte[] result = br.ReadBytes((int)resp.ContentLength);
                    success = true;
                    return result;
                }
            }
            catch
            {
                success = false;
                return null;
            }
        }

		public static byte[] sendRecv(string url, byte[] data, int timeoutMs, out bool success)
        {
            try
            {
                HttpWebRequest req = HttpWebRequest.CreateHttp(url);
				req.Method = "POST";
				req.ContentType = "multipart/form-data";
                req.ContentLength = data.Length;
                req.Timeout = timeoutMs;

                using (var stream = req.GetRequestStream())
                    stream.Write(data, 0, data.Length);

                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                var resStream = resp.GetResponseStream();
                using (BinaryReader br = new BinaryReader(resStream))
                {
                    byte[] result = br.ReadBytes((int)resp.ContentLength);
                    success = true;
                    return result;
                }
            }
            catch {
                success = false; 
                return null;
            }
        }

        public static byte[] buildRequest(String url, List<ChainNodeInfo> chain)
        {
            byte[] data = null;

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                data = packRequest(url, data, chain[i].PublicKey);
                url = chain[i].Url;
            }
            return data;
        }

        public static byte[] packRequest(String url, byte[] data, RSAParameters key)
        {
            if (data == null)
                data = new byte[0];

            byte[] encodedUrl = Encoding.UTF8.GetBytes(url);

            using (MemoryStream ms = new MemoryStream(4 + encodedUrl.Length + data.Length))
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write(encodedUrl.Length);
                    bw.Write(encodedUrl);
                    bw.Write(data);

                    return Crypto.encrypt(ms.ToArray(), key);
                }
            }
        }

        public static void unpackRequest(byte[] packed, RSAParameters key, out String unpackedUrl, out byte[] unpackedData)
        {
            using (MemoryStream ms = new MemoryStream(Crypto.decrypt(packed, key)))
            {
                using (BinaryReader br = new BinaryReader(ms))
                {
                    unpackedUrl = Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
                    unpackedData = br.ReadBytes((int)(ms.Length - ms.Position));
                }
            }
        }
        
        public static byte[] buildRequest(String url, List<ChainNodeInfo> chain, RSAParameters originPublicKey)
        {
            byte[] data = null;

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                data = packRequest(url, data, chain[i].PublicKey, originPublicKey);
                url = chain[i].Url;
            }
            return data;
        }

        public static byte[] packRequest(String url, byte[] data, RSAParameters keyForEncryption, RSAParameters originPublicKey)
        {
            if (data == null)
                data = new byte[0];

            byte[] encodedUrl = Encoding.UTF8.GetBytes(url);
            byte[] originPublicKeyBytes = Crypto.exportKeyBinary(originPublicKey);

            using (MemoryStream ms = new MemoryStream(8 + encodedUrl.Length + originPublicKeyBytes.Length + data.Length))
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write(encodedUrl.Length);
                    bw.Write(encodedUrl);
                    bw.Write(originPublicKeyBytes.Length);
                    bw.Write(originPublicKeyBytes);
                    bw.Write(data);

                    return Crypto.encrypt(ms.ToArray(), keyForEncryption);
                }
            }
        }

        public static void unpackRequest(byte[] packed, RSAParameters key, out String unpackedUrl, out RSAParameters originPublicKey, out byte[] unpackedData)
        {
            using (MemoryStream ms = new MemoryStream(Crypto.decrypt(packed, key)))
            {
                using (BinaryReader br = new BinaryReader(ms))
                {
                    unpackedUrl     = Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
                    originPublicKey = Crypto.importKey(br.ReadBytes(br.ReadInt32()));
                    unpackedData    = br.ReadBytes((int)(ms.Length - ms.Position));
                }
            }
        }
    }
}
