using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Results;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Maintenance.Services;

// Local disk under wwwroot. Deliberately the simplest thing that works: the deployment is a single
// IIS site, so files land next to the app and are served statically. Swapping this for S3 or blob
// storage means replacing this class and nothing else — the entity stores a URL either way.
public sealed class MaintenanceAttachmentService : IMaintenanceAttachmentService
{
    private const long MaxBytes = 25 * 1024 * 1024;

    // Extension allow-list, not a content-type check: the browser-supplied content type is
    // caller-controlled and proves nothing.
    private static readonly Dictionary<string, AttachmentKind> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = AttachmentKind.Image,
        [".jpeg"] = AttachmentKind.Image,
        [".png"] = AttachmentKind.Image,
        [".webp"] = AttachmentKind.Image,
        [".heic"] = AttachmentKind.Image,
        [".pdf"] = AttachmentKind.Pdf,
        [".mp4"] = AttachmentKind.Video,
        [".mov"] = AttachmentKind.Video
    };

    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpContextAccessor _http;

    public MaintenanceAttachmentService(
        ModaarDbContext db, TimeProvider time, IWebHostEnvironment env, IHttpContextAccessor http)
    {
        _db = db;
        _time = time;
        _env = env;
        _http = http;
    }

    public async Task<Result<AttachmentDto>> UploadAsync(
        Guid uploaderUserId, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return Result<AttachmentDto>.Fail(AppErrorCode.ValidationFailed, "The file is empty.");

        if (file.Length > MaxBytes)
            return Result<AttachmentDto>.Fail(AppErrorCode.ValidationFailed, "The file exceeds 25 MB.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !Allowed.TryGetValue(extension, out var kind))
            return Result<AttachmentDto>.Fail(AppErrorCode.ValidationFailed, "Unsupported file type.");

        var id = Guid.NewGuid();

        // The stored name is derived entirely from a server-generated id. The client's file name
        // is kept only as a display label and never touches the file system — it is the classic
        // path-traversal vector.
        var storedName = $"{id:N}{extension.ToLowerInvariant()}";
        var folder = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"),
            "uploads", "maintenance");

        Directory.CreateDirectory(folder);

        var fullPath = Path.Combine(folder, storedName);
        await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
            await file.CopyToAsync(stream, ct);

        var request = _http.HttpContext!.Request;
        var url = $"{request.Scheme}://{request.Host}/uploads/maintenance/{storedName}";

        var attachment = new MaintenanceAttachment
        {
            Id = id,
            RequestId = null,
            UploadedByUserId = uploaderUserId,
            Url = url,
            FileName = Path.GetFileName(file.FileName),
            Kind = kind,
            SizeBytes = file.Length,
            CreatedAt = _time.GetUtcNow()
        };

        _db.MaintenanceAttachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        return Result<AttachmentDto>.Ok(new AttachmentDto
        {
            Id = attachment.Id,
            Url = attachment.Url,
            FileName = attachment.FileName,
            Kind = attachment.Kind
        });
    }
}
