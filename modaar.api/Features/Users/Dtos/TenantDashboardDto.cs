using modaar.api.Features.Maintenance.Enums;
using modaar.api.Features.Payments.Dtos;

namespace modaar.api.Features.Users.Dtos;

// Everything the tenant home screen needs in one call.
public record TenantDashboardDto
{
    // Null until the profile is completed; the app greets them without a name rather than
    // blocking the screen.
    public string? UserName { get; init; }

    // Null when the tenant has no active contract — an account can sit at AccountType.Tenant
    // before anyone has put them in a unit. The app shows an empty state.
    public TenantUnitCardDto? CurrentUnit { get; init; }

    // Which maintenance tiles to offer. Server-driven so a category can be switched off without
    // shipping a new build.
    public required IReadOnlyList<MaintenanceServiceType> MaintenanceCategories { get; init; }

    // Most recent payments only. The full history is GET /payments.
    public required IReadOnlyList<PaymentDto> PaymentsPreview { get; init; }
}
