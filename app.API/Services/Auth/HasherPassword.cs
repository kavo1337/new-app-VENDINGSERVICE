using System.Security.Cryptography;
using System.Text;

namespace app.API.Services.Auth
{
    public class HasherPassword
    {
        private const int Iteration = 100_000;
        private const int KeySizeBytes = 32;
        public static string HashPassword(string password, string salt)
        {
            var saltBytes = GetBytesSalt(salt);
            var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                Iteration,
                HashAlgorithmName.SHA256,
                KeySizeBytes);
            return Convert.ToBase64String(hashBytes);
        }
        public static bool VerifyPassword(string password, string hash, string? salt)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException();
            }
            if (string.IsNullOrEmpty(hash))
            {
                return false;
            }
            if (string.IsNullOrEmpty(salt))
            {
                if (SlowEquals(password, hash))
                {
                    return true;
                }
                var simpleHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
                return SlowEquals(simpleHash, hash);
            }
            try
            {
                var computed = HashPassword(password, salt);
                return SlowEquals(computed, hash);
            }
            catch
            {
                return false;
            }
        }

        public static bool SlowEquals(string a, string b)
        {
            if (a == null || b == null)
                return a == b;
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            var diff = aBytes.Length ^ bBytes.Length;
            var lenght = Math.Min(aBytes.Length, bBytes.Length);

            for (int i = 0; i < lenght; i++)
            {
                diff |= aBytes[i] ^ bBytes[i];
            }
            return diff == 0;
        }

        public static byte[] GetBytesSalt(string salt)
        {
            try
            {
                return Convert.FromBase64String(salt);
            }
            catch (FormatException)
            {
                return Encoding.UTF8.GetBytes(salt);
            }
        }
    }
}
