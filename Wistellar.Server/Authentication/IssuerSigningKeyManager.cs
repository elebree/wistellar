using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace Wistellar.Server.Authentication
{
    public static class IssuerSigningKeyManager
    {
        private const string DefaultKeyFileName = "issuer_signing_key.txt";
        private const int KeyLength = 64; // 512 bits for HMAC-SHA256

        public static string GetKeyFilePath(string basePath, string? relativePath = null)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return Path.Combine(basePath, DefaultKeyFileName);
            }

            // If relativePath is a full path, use it as is
            if (Path.IsPathRooted(relativePath))
            {
                return relativePath;
            }

            // Otherwise, combine with base path
            return Path.Combine(basePath, relativePath);
        }

        public static string ReadOrGenerateKey(string keyFilePath)
        {
            // Ensure directory exists. GetDirectoryName returns null for a rooted path with no
            // parent and empty for a bare filename; neither case needs a directory created.
            var directory = Path.GetDirectoryName(keyFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Check if key file exists
            if (File.Exists(keyFilePath))
            {
                return File.ReadAllText(keyFilePath).Trim();
            }

            // Generate new key if file doesn't exist
            var newKey = GenerateSecureKey();
            File.WriteAllText(keyFilePath, newKey);
            return newKey;
        }

        public static SymmetricSecurityKey GetSymmetricSecurityKey(string keyFilePath)
        {
            var key = ReadOrGenerateKey(keyFilePath);
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        }

        private static string GenerateSecureKey()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[KeyLength];
                rng.GetBytes(bytes);

                // Convert to base64 and make it URL safe. 64 random bytes encode to 86 base64
                // characters, comfortably above the 32 bytes HMAC-SHA256 requires.
                var base64 = Convert.ToBase64String(bytes)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .TrimEnd('=');

                return base64.Substring(0, 64);
            }
        }
    }
}