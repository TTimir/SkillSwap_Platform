using System.Security.Cryptography;

namespace SkillSwap_Platform.HelperClass
{
    public class PasswordHelper
    {
        private const int Iterations = 150000; // Recommended for security

        public static string GenerateSalt()
        {
            byte[] saltBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }

        public static string HashPassword(string password, string salt)
        {
            using (var deriveBytes = new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), Iterations, HashAlgorithmName.SHA256))
            {
                return Convert.ToBase64String(deriveBytes.GetBytes(32));
            }
        }

        public static bool VerifyPassword(string enteredPassword, string storedHash, string salt)
        {
            string computedHash = HashPassword(enteredPassword, salt);
            return storedHash == computedHash;
        }
    }
}
