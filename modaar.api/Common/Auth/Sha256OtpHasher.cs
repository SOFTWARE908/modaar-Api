using System.Security.Cryptography;
using System.Text;

namespace modaar.api.Common.Auth;

public sealed class Sha256OtpHasher : IOtpHasher
{
    public string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    public bool Verify(string code, string hash)
    {
        byte[] stored;
        try
        {
            stored = Convert.FromHexString(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        var computed = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return CryptographicOperations.FixedTimeEquals(computed, stored);
    }
}
