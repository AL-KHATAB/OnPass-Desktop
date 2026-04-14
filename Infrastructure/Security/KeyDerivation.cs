using System;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace OnPass.Infrastructure.Security
{
    // Derives stable encryption keys from the user's master password and
    // generates fresh salts so password-based keys are not reused across users.
    public static class KeyDerivation
    {

        // A high PBKDF2 iteration count slows offline guessing without changing
        // the rest of the app's storage format.
        private const int DEFAULT_ITERATIONS = 1000000;

        // Uses PBKDF2-SHA256 to turn the master password into AES key material for vault encryption.
        public static byte[] DeriveKey(string password, byte[] salt, int iterations = DEFAULT_ITERATIONS, int keySize = 32)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(keySize);
            }
        }

        // Generates a random salt so identical passwords do not derive identical keys.
        public static byte[] GenerateSalt(int size = 16)
        {
            byte[] salt = new byte[size];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }
    }
}
