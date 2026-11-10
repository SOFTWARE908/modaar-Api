using Microsoft.Extensions.Options;

namespace modaar.api.Common.Auth;

public sealed class LoggingOtpDeliverySender : IOtpDeliverySender
{
    private readonly ILogger<LoggingOtpDeliverySender> _logger;
    private readonly IOptions<OtpSettings> _otpSettings;

    public LoggingOtpDeliverySender(ILogger<LoggingOtpDeliverySender> logger, IOptions<OtpSettings> otpSettings)
    {
        _logger = logger;
        _otpSettings = otpSettings;
    }

    public Task SendAsync(string destination, string code, OtpDeliveryChannel channel, CancellationToken ct)
    {
        // Defense in depth: refuse to log raw OTP codes in production even if this dev sender is mistakenly registered.
        if (!_otpSettings.Value.UseFixedCode)
        {
            _logger.LogWarning("Refusing to deliver OTP via LoggingOtpDeliverySender because OtpSettings:UseFixedCode is false. Wire a real {Channel} provider for production.", channel);
            return Task.CompletedTask;
        }

        _logger.LogWarning("[OTP-DEV] Would deliver code {Code} to {Destination} via {Channel}", code, destination, channel);
        return Task.CompletedTask;
    }
}
