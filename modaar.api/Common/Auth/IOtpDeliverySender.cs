namespace modaar.api.Common.Auth;

public interface IOtpDeliverySender
{
    Task SendAsync(string destination, string code, OtpDeliveryChannel channel, CancellationToken ct);
}
