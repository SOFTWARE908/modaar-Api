using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Enums;

namespace modaar.api.Features.Maintenance.Services;

public interface IMaintenanceService
{
    // Scoped by the caller's account type: a tenant sees their own, an owner sees requests on
    // properties they own, a broker sees the properties they manage.
    Task<Result<PagedResultDto<MaintenanceRequestListItemDto>>> ListAsync(
        Guid callerUserId, MaintenanceListQueryDto query, CancellationToken ct);

    Task<Result<MaintenanceRequestDetailsDto>> GetDetailsAsync(
        Guid requestId, Guid callerUserId, CancellationToken ct);

    Task<Result<CreateMaintenanceRequestResponseDto>> CreateAsync(
        Guid tenantUserId, CreateMaintenanceRequestDto request, CancellationToken ct);
}

public interface IMaintenanceAttachmentService
{
    Task<Result<AttachmentDto>> UploadAsync(Guid uploaderUserId, IFormFile file, CancellationToken ct);
}

// The client shows a time range for each slot; the enum only carries a start hour. Kept in one
// place so the list summary and the details header can never disagree.
public static class VisitSlots
{
    public const int MaxAttachments = 8;

    public static (TimeOnly Start, TimeOnly End) Range(MaintenanceTimeSlot slot) => slot switch
    {
        MaintenanceTimeSlot.Morning   => (new TimeOnly(8, 0),  new TimeOnly(14, 0)),
        MaintenanceTimeSlot.Afternoon => (new TimeOnly(14, 0), new TimeOnly(17, 0)),
        // The spec gives evening a start hour but no end; 21:00 is an assumption worth confirming.
        MaintenanceTimeSlot.Evening   => (new TimeOnly(17, 0), new TimeOnly(21, 0)),
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public static string Format(MaintenanceTimeSlot slot)
    {
        var (start, end) = Range(slot);
        // En dash with spaces, matching the client's "08:00 – 14:00".
        return $"{start:HH\\:mm} – {end:HH\\:mm}";
    }
}
