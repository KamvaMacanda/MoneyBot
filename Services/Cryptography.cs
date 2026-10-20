using System.Security.Cryptography;
using System.Text;

namespace MoneyBot.Services
{
    public class Cryptography
    {

        // AES-GCM / SHA-256 reversible or envelope encryption for user telemetry messages
        /// <summary>
        /// hode this
        /// </summary>
        private static readonly byte[] AesKey = SHA256.HashData(Encoding.UTF8.GetBytes("MoneyBotSecureMessageEncryptionKey2026!"));

        public static string EncryptMessage(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            using var aes = Aes.Create();
            aes.Key = AesKey;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            var combined = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, combined, aes.IV.Length, cipherBytes.Length);

            return "ENC:" + Convert.ToBase64String(combined);
        }

        public static string DecryptMessageIfEncrypted(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || !message.StartsWith("ENC:"))
                return message;

            try
            {
                var payload = Convert.FromBase64String(message[4..]);
                using var aes = Aes.Create();
                aes.Key = AesKey;

                var iv = new byte[aes.BlockSize / 8];
                var cipherBytes = new byte[payload.Length - iv.Length];
                Buffer.BlockCopy(payload, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(payload, iv.Length, cipherBytes, 0, cipherBytes.Length);

                using var decryptor = aes.CreateDecryptor(aes.Key, iv);
                var decrypted = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return message;
            }
        }
    }
}
