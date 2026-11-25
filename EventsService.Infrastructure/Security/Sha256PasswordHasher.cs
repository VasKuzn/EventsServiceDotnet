using EventsService.Application.Interfaces.Auth;
using System.Security.Cryptography;
using System.Text;

namespace EventsService.Infrastructure.Security
{
    public sealed class Sha256PasswordHasher : IPasswordHasher
    {
        public string Hash(string password)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        }

        public bool Verify(string password, string passwordHash)
        {
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(Hash(password)), Encoding.ASCII.GetBytes(passwordHash));
        }
    }
}
