using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OnionRouting
{
    public static class Crypto
    {
        public static byte[] encrypt(byte[] msg, RSAParameters rsaPublicKey)
        {
            using (Aes aes = Aes.Create())
            {
                aes.GenerateKey();
                aes.GenerateIV();

                byte[] aesKeyInformation = new byte[aes.Key.Length + aes.IV.Length];
                Array.Copy(aes.Key, aesKeyInformation, aes.Key.Length);
                Array.Copy(aes.IV, 0, aesKeyInformation, aes.Key.Length, aes.IV.Length);

                byte[] encryptedAesKeyInformation;
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.ImportParameters(rsaPublicKey);
                    encryptedAesKeyInformation = rsa.Encrypt(aesKeyInformation, true);
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    using (BinaryWriter bw = new BinaryWriter(ms))
                    {
                        bw.Write(encryptedAesKeyInformation.Length);
                        bw.Write(encryptedAesKeyInformation);

                        var encryptor = aes.CreateEncryptor();
                        using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                            cs.Write(msg, 0, msg.Length);
                    }

                    return ms.ToArray();
                }
            }
        }

        public static byte[] decrypt(byte[] encryptedMessage, RSAParameters rsaPrivateKey)
        {
            using (MemoryStream ms = new MemoryStream(encryptedMessage))
            {
                using (BinaryReader br = new BinaryReader(ms))
                {
                    byte[] encryptedAesKeyInformation = br.ReadBytes(br.ReadInt32());
                    byte[] aesKeyInformation;
                    using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                    {
                        rsa.ImportParameters(rsaPrivateKey);
                        aesKeyInformation = rsa.Decrypt(encryptedAesKeyInformation, true);
                    }

                    using (Aes aes = Aes.Create())
                    {
                        byte[] aesKey = new byte[32];
                        byte[] aesIV = new byte[16];
                        Array.Copy(aesKeyInformation, aesKey, 32);
                        Array.Copy(aesKeyInformation, 32, aesIV, 0, 16);

                        aes.Key = aesKey; aes.IV = aesIV;
                        var decryptor = aes.CreateDecryptor();

                        using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        {
                            byte[] msg = new byte[ms.Length - ms.Position];
                            int length = cs.Read(msg, 0, msg.Length);
                            Array.Resize(ref msg, length);
                            return msg;
                        }
                    }
                }
            }
        }

        public static RSAKeyPair generateKey(int keysize = 2048)
        {
            using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider(keysize))
            {
                var privateKey = RSA.ExportParameters(true);
                var publicKey = RSA.ExportParameters(false);
                var privateKeyStr = RSA.ToXmlString(true);
                var publicKeyStr = RSA.ToXmlString(false);

                return new RSAKeyPair(privateKey, publicKey, privateKeyStr, publicKeyStr);
            }
        }

        public static RSAParameters parseKey(string keyXML)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(keyXML);
                return rsa.ExportParameters(false);
            }
        }

        public static string exportKey(RSAParameters key, bool includePrivate = false)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.ImportParameters(key);
                return rsa.ToXmlString(includePrivate);
            }
        }
    }
}
