using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace M_A_G_A.Helpers
{
    /// <summary>
    /// AES-256-CBC encryption with PBKDF2-SHA1 key derivation.
    /// Wire format: [salt(16 bytes)][IV(16 bytes)][ciphertext].
    /// </summary>
    public static class EncryptionHelper
    {
        private const int SaltSize   = 16;
        private const int Iterations = 100_000;
        private const int KeySize    = 32;   // 256-bit AES key

        /// <summary>Encrypts plaintext bytes with a password.</summary>
        public static byte[] Encrypt(byte[] plaintext, string password)
        {
            var salt = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(salt);

            using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations))
            using (var aes = new AesCryptoServiceProvider())
            {
                aes.Key = kdf.GetBytes(KeySize);
                aes.GenerateIV();
                using (var ms = new MemoryStream())
                {
                    ms.Write(salt, 0, salt.Length);
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        cs.Write(plaintext, 0, plaintext.Length);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Decrypts data previously produced by Encrypt().
        /// Returns null when the password is wrong or the data is corrupted.
        /// </summary>
        public static byte[] Decrypt(byte[] data, string password)
        {
            try
            {
                using (var ms = new MemoryStream(data))
                {
                    var salt = new byte[SaltSize];
                    ms.Read(salt, 0, salt.Length);
                    var iv = new byte[16];
                    ms.Read(iv, 0, iv.Length);

                    using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations))
                    using (var aes = new AesCryptoServiceProvider())
                    {
                        aes.Key = kdf.GetBytes(KeySize);
                        aes.IV  = iv;
                        using (var cs     = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                        using (var output = new MemoryStream())
                        {
                            cs.CopyTo(output);
                            return output.ToArray();
                        }
                    }
                }
            }
            catch { return null; }
        }

        /// <summary>Returns a base64-encoded SHA-256 hash of the password.</summary>
        public static string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }

        /// <summary>Verifies a plain-text password against a stored hash.</summary>
        public static bool VerifyPassword(string password, string hash)
            => !string.IsNullOrEmpty(password) && HashPassword(password) == hash;
    }
}
