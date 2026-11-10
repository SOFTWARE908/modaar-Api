using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace modaar.api.Common.Auth;

public sealed class OtpCodeGenerator : IOtpCodeGenerator
{
    private static readonly char[] Digits = "0123456789".ToCharArray();
    private readonly IOptions<OtpSettings> _settings;

    public OtpCodeGenerator(IOptions<OtpSettings> settings)
    {
        _settings = settings;
    }

    public string Generate()
    {
        var s = _settings.Value;
        if (s.UseFixedCode)
            return s.FixedCode;

        return RandomNumberGenerator.GetString(Digits, s.LengthDigits);
    }
}
