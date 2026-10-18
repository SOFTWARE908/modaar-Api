using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Localization;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Persistence;

namespace modaar.api.Features.Maintenance.Services;

// The list card, shared by GET /maintenance-requests and the dashboards so the two can never
// drift apart. The caller scopes, orders and pages; this only projects.
public static class MaintenanceListProjection
{
    public static async Task<List<MaintenanceRequestListItemDto>> ToListItemsAsync(
        IQueryable<MaintenanceRequest> requests, ModaarDbContext db, IRequestLanguage language, CancellationToken ct)
    {
        var rows = await requests
            .Select(m => new
            {
                m.Id,
                m.RequestNumber,
                m.Title,
                ServiceType = db.MaintenanceServiceTypes
                    .Where(t => t.Id == m.ServiceTypeId)
                    .Select(t => new { t.Id, t.Code, t.NameAr, t.NameEn, t.IconUrl, t.SortOrder })
                    .FirstOrDefault(),
                m.Status,
                m.Priority,
                m.CreatedAt,
                m.ScheduledVisitDate,
                m.ScheduledTimeSlot,
                m.PreferredVisitDate,
                m.PreferredTimeSlot,
                m.QuoteRequired,
                HasQuote = m.QuotedAt != null,
                IsRated = db.MaintenanceRatings.Any(r => r.RequestId == m.Id),
                m.PropertyId,
                Property = db.Properties
                    .Where(p => p.Id == m.PropertyId)
                    .Select(p => new
                    {
                        p.Title,
                        HasBroker = p.BrokerId != null,
                        OwnerName = db.Users.Where(u => u.Id == p.OwnerUserId).Select(u => u.FullName).FirstOrDefault()
                    })
                    .FirstOrDefault(),
                Technician = db.Technicians
                    .Where(t => t.Id == m.AssignedTechnicianId)
                    .Select(t => new
                    {
                        t.Id,
                        t.RoleAr,
                        t.RoleEn,
                        t.RatingAverage,
                        User = db.Users.Where(u => u.Id == t.UserId)
                            .Select(u => new { u.FullName, u.ProfileImageUrl, u.CountryCode, u.PhoneNumber })
                            .FirstOrDefault()
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Select(m =>
        {
            var date = m.ScheduledVisitDate ?? m.PreferredVisitDate;
            var slot = m.ScheduledTimeSlot ?? m.PreferredTimeSlot;
            var categoryName = m.ServiceType is null
                ? string.Empty
                : language.Pick(m.ServiceType.NameAr, m.ServiceType.NameEn);

            var stage = MaintenanceStages.Derive(m.Status, m.Property?.HasBroker ?? false, m.QuoteRequired, m.HasQuote);

            return new MaintenanceRequestListItemDto
            {
                Id = m.Id,
                RequestCode = m.RequestNumber,
                Priority = m.Priority,
                ServiceType = m.ServiceType is null
                    ? null!
                    : new MaintenanceServiceTypeDto
                    {
                        Id = m.ServiceType.Id,
                        Code = m.ServiceType.Code,
                        Name = categoryName,
                        IconUrl = m.ServiceType.IconUrl,
                        SortOrder = m.ServiceType.SortOrder
                    },
                Title = string.IsNullOrWhiteSpace(m.Title) ? categoryName : m.Title,
                Status = m.Status,
                Stage = stage,
                NextActionLabel = MaintenanceStages.NextActionLabel(
                    stage, m.Technician is not null, date, m.IsRated, language),
                CreatedAt = m.CreatedAt,
                PropertyId = m.PropertyId,
                PropertyTitle = m.Property?.Title ?? string.Empty,
                OwnerName = m.Property?.OwnerName,
                VisitSummary = date is { } d && slot is { } s
                    ? $"{d.ToString("d MMMM yyyy", language.Culture)} · {MaintenanceTimeSlotNames.Label(s, language)}"
                    : null,
                AssignedTechnician = m.Technician is null ? null : new TechnicianDto
                {
                    Id = m.Technician.Id,
                    Name = m.Technician.User?.FullName ?? string.Empty,
                    Role = language.Pick(m.Technician.RoleAr, m.Technician.RoleEn),
                    ImageUrl = m.Technician.User?.ProfileImageUrl,
                    AverageRating = m.Technician.RatingAverage,
                    PhoneNumber = m.Technician.User is null ? null : m.Technician.User.CountryCode + m.Technician.User.PhoneNumber
                }
            };
        }).ToList();
    }
}
