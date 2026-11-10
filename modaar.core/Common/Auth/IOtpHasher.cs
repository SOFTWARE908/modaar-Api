namespace modaar.api.Common.Auth;

public interface IOtpHasher
{
    string Hash(string code);
    bool Verify(string code, string hash);
}
