using System;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace Core.Security
{
    public static class Encryption
    {
        public enum HashAlgorithms { MD5, SHA1, SHA256, SHA512 }

        /// <summary>
        /// Compute a hash from a string using the specified algorithm
        /// </summary>
        public static string ComputeHash(string toHash, HashAlgorithms algorithm = HashAlgorithms.MD5)
        {
            byte[] toHashBytes = Encoding.UTF8.GetBytes(toHash);

            HashAlgorithm hash;
            switch (algorithm)
            {
                case HashAlgorithms.SHA1:
                    {
                        hash = new SHA1Managed();
                        break;
                    }
                case HashAlgorithms.SHA256:
                    {
                        hash = new SHA256Managed();
                        break;
                    }
                case HashAlgorithms.SHA512:
                    {
                        hash = new SHA512Managed();
                        break;
                    }
                default:
                    {
                        hash = new MD5CryptoServiceProvider();
                        break;
                    }
            }

            byte[] hashBytes = hash.ComputeHash(toHashBytes);

            StringBuilder sb = new StringBuilder();
            for (int i = 0, len = hashBytes.Length; i < len; i++)
                sb.Append(hashBytes[i].ToString("x2"));

            return sb.ToString();
        }

        /// <summary>
        /// Encrypt a byte array using AES encryption
        /// </summary>
        /// <param name="input">Bytes to encrypt</param>
        /// <param name="key">Secret key to use</param>
        /// <returns>Encrypted bytes</returns>
        public static byte[] Encrypt(byte[] input, string key)
        {
            using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
            {
                aes.Key = UTF8Encoding.UTF8.GetBytes(key);
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    using (MemoryStream output = new MemoryStream())
                    {
                        output.Write(aes.IV, 0, 16);

                        using (CryptoStream csEncrypt = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
                        {
                            csEncrypt.Write(input, 0, input.Length);                            
                        }

                        return output.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// Encrypt a string using AES encryption
        /// </summary>
        /// <param name="input">String to encrypt</param>
        /// <param name="key">Secret key to use</param>
        /// <returns>Encrypted bytes</returns>
        public static byte[] Encrypt(string input, string key)
        {
            return Encrypt(UTF8Encoding.UTF8.GetBytes(input), key);
        }

        /// <summary>
        /// Encrypt a string using AES encryption
        /// </summary>
        /// <param name="input">String to encrypt</param>
        /// <param name="key">Secret key to use</param>
        /// <returns>A Base64 string of the encrypted bytes</returns>
        public static string EncryptToBase64String(string input, string key)
        {
            byte[] encrypted = Encrypt(input, key);
            return Convert.ToBase64String(encrypted, 0, encrypted.Length);
        }

        /// <summary>
        /// Decrypt a bytes using AES encryption
        /// </summary>
        /// <param name="input">Bytes to decrypt</param>
        /// <param name="key">Secret key to use</param>
        /// <returns>The decrypted bytes</returns>
        public static byte[] Decrypt(byte[] input, string key)
        {
            using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
            {
                aes.Key = UTF8Encoding.UTF8.GetBytes(key);
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                                
                using (MemoryStream msDecrypt = new MemoryStream(input))
                using (MemoryStream output = new MemoryStream())
                {
                    byte[] iv = new byte[16];
                    msDecrypt.Read(iv, 0, 16);
                    aes.IV = iv;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            csDecrypt.CopyTo(output);
                        }
                    }

                    return output.ToArray();
                }
            }
        }

        /// <summary>
        /// Decrypt a Base64 string using AES encryption
        /// </summary>
        /// <param name="input">The string to decrypt</param>
        /// <param name="key">Secret key to use</param>
        /// <returns>The decrypted string</returns>
        public static string DecryptFromBase64String(string input, string key)
        {
            byte[] decrypted = Decrypt(Convert.FromBase64String(input), key);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}
