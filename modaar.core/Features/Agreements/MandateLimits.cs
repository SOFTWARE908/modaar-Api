using Microsoft.EntityFrameworkCore;
using modaar.api.Features.Agreements.Entities;
using modaar.api.Persistence;

namespace modaar.api.Features.Agreements;

// The maintenance auto-approve limit a unit runs under: the one on the active mandate holding it.
// No active mandate (or a mandate without a limit) → null, so every quote goes to the owner.
public static class MandateLimits
{
    public static Task<decimal?> ForPropertyAsync(ModaarDbContext db, Guid propertyId, CancellationToken ct) =>
        db.AgreementProperties
            .AsNoTracking()
            .Where(u => u.PropertyId == propertyId && u.ActiveFrom != null && u.ExitedOn == null)
            .Join(db.ManagementAgreements.Where(a => a.Status == ManagementAgreementStatus.Active),
                u => u.AgreementId, a => a.Id, (u, a) => a.AutoApproveLimit)
            .FirstOrDefaultAsync(ct);
}
