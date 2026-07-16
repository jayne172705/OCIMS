using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Generators;

namespace OCIMS
{
    /// <summary>
    /// bcrypt hashing via BouncyCastle (already referenced by the project).
    /// Stored hashes start with "$2"; anything else is treated as a legacy
    /// plaintext password and upgraded on the next successful login.
    /// </summary>
    public static class PasswordHasher
    {
        private const int Cost = 10;

        public static string Hash(string password)
        {
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);
            return OpenBsdBCrypt.Generate(password.ToCharArray(), salt, Cost);
        }

        public static bool IsHashed(string stored)
        {
            return stored != null && stored.StartsWith("$2");
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored)) return false;
            if (IsHashed(stored))
            {
                try { return OpenBsdBCrypt.CheckPassword(stored, password.ToCharArray()); }
                catch { return false; }
            }
            // Legacy plaintext row — ordinal comparison (case-sensitive).
            return string.Equals(stored, password, System.StringComparison.Ordinal);
        }
    }
}
