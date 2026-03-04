using System;
using System.IO;
using System.Security.Cryptography;

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

        /// <summary>
        /// Returns a salted PBKDF2 hash of the password suitable for storage.
        /// Format: base64([salt(16)][hash(32)]).
        /// </summary>
        public static string HashPassword(string password)
        {
            var salt = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(salt);
            using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                var hash = kdf.GetBytes(32);
                var result = new byte[salt.Length + hash.Length];
                Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
                Buffer.BlockCopy(hash, 0, result, salt.Length, hash.Length);
                return Convert.ToBase64String(result);
            }
        }

        /// <summary>Verifies a plain-text password against a stored PBKDF2 hash.</summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash)) return false;
            try
            {
                var data = Convert.FromBase64String(storedHash);
                if (data.Length < 48) return false;   // 16 salt + 32 hash
                var salt = new byte[16];
                Buffer.BlockCopy(data, 0, salt, 0, 16);
                var stored = new byte[32];
                Buffer.BlockCopy(data, 16, stored, 0, 32);
                using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations))
                {
                    var computed = kdf.GetBytes(32);
                    // Constant-time comparison
                    int diff = 0;
                    for (int i = 0; i < 32; i++) diff |= stored[i] ^ computed[i];
                    return diff == 0;
                }
            }
            catch { return false; }
        }
    }
}
