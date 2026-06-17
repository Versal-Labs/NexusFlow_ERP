using System.Security.Cryptography;
using System.Text;

namespace NexusFlow.Infrastructure.Installation
{
    internal static class InstallationSecretFingerprint
    {
        public static string Create(string value)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash)[..12];
        }
    }
}
