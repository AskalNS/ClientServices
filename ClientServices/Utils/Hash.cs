using System.Security.Cryptography;

namespace ClientService.Utils
{
    public class Hash
    {

        private const int SaltSize = 16; // 128-bit salt
        private const int KeySize = 32;  // 256-bit key
        private const int Iterations = 10000; // Number of iterations

        public static string HashPassword(string password)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] salt = new byte[SaltSize];
                rng.GetBytes(salt);

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    byte[] hash = pbkdf2.GetBytes(KeySize);

                    // Combine salt and hash
                    byte[] hashBytes = new byte[SaltSize + KeySize];
                    Array.Copy(salt, 0, hashBytes, 0, SaltSize);
                    Array.Copy(hash, 0, hashBytes, SaltSize, KeySize);

                    // Convert to base64 string
                    string base64Hash = Convert.ToBase64String(hashBytes);

                    // Combine iterations and base64 hash
                    return $"{Iterations}.{base64Hash}";
                }
            }
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            // Split iterations and base64 hash
            var parts = hashedPassword.Split('.', 2);
            if (parts.Length != 2)
            {
                return false;
            }

            // Extract iterations and base64 hash
            var iterations = Convert.ToInt32(parts[0]);
            var base64Hash = parts[1];

            // Convert base64 hash to byte array
            var hashBytes = Convert.FromBase64String(base64Hash);

            // Extract salt and hash
            var salt = new byte[SaltSize];
            var hash = new byte[KeySize];
            Array.Copy(hashBytes, 0, salt, 0, SaltSize);
            Array.Copy(hashBytes, SaltSize, hash, 0, KeySize);

            // Hash the input password with the same iterations, salt, and key size
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                byte[] testHash = pbkdf2.GetBytes(KeySize);

                // Compare the hash
                int result = 0;
                for (int i = 0; i < KeySize; i++)
                {
                    result |= testHash[i] ^ hash[i];
                }

                return result == 0;
            }
        }

    }
}
