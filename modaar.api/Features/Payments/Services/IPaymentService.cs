using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Payments.Dtos;

namespace modaar.api.Features.Payments.Services;

public interface IPaymentService
{
    // Scoped by account type: a tenant sees what they paid, an owner what was paid on their
    // properties, a broker what was paid on the ones they manage.
    Task<Result<PagedResultDto<PaymentDto>>> ListAsync(
        Guid callerUserId, PaymentListQueryDto query, CancellationToken ct);

    Task<Result<InvoiceDetailsDto>> GetInvoiceAsync(
        Guid paymentId, Guid callerUserId, CancellationToken ct);

    // Returns the stored document URL. Generation is not implemented.
    Task<Result<string>> GetInvoiceUrlAsync(Guid paymentId, Guid callerUserId, CancellationToken ct);
}
