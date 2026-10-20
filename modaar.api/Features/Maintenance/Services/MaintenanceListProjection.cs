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
        IQueryable<MaintenanceRequest> requests, ModaarDbContext db, IRequestLanguage language,
        Guid callerUserId, CancellationToken ct)
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
                m.ScheduledVisitTime,
                m.ScheduledTimeSlot,
                m.PreferredVisitDate,
                m.PreferredTimeSlot,
                m.QuoteRequired,
                HasQuote = m.QuotedAt != null,
                QuoteTotal = m.QuotedAt != null ? (m.QuotePartsCost ?? 0) + (m.QuoteLaborCost ?? 0) : (decimal?)null,
                QuoteApproved = m.QuoteApprovedAt != null,
                m.SubscriptionPlan,
                m.TenantUserId,
                TenantName = db.Users.Where(u => u.Id == m.TenantUserId).Select(u => u.FullName).FirstOrDefault(),
                Rating = db.MaintenanceRatings
                    .Where(r => r.RequestId == m.Id)
                    .Select(r => new { r.Stars, r.TechnicianRating, r.RepairQualityRating, r.ResponseSpeedRating, r.BrokerCommunicationRating })
                    .FirstOrDefault(),
                m.PropertyId,
                Property = db.Properties
                    .Where(p => p.Id == m.PropertyId)
                    .Select(p => new
                    {
                        p.Title,
                        p.OwnerUserId,
                        HasBroker = p.BrokerId != null,
                        BrokerUserId = db.Brokers.Where(b => b.Id == p.BrokerId).Select(b => (Guid?)b.UserId).FirstOrDefault(),
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
                            .FirstOrDefault(),
                        Specialties = db.TechnicianServiceTypes
                            .Where(s => s.TechnicianId == t.Id)
                            .Join(db.MaintenanceServiceTypes, s => s.ServiceTypeId, st => st.Id,
                                (s, st) => new { st.NameAr, st.NameEn, st.SortOrder })
                            .ToList()
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

            var facts = new MaintenanceFacts(
                m.Status, m.SubscriptionPlan, m.Property?.HasBroker ?? false, m.QuoteRequired, m.HasQuote,
                m.QuoteApproved, m.Technician is not null, m.Rating is not null);
            var stage = MaintenanceStages.Derive(facts);

            var isTenant = m.TenantUserId == callerUserId;
            var caller = new MaintenanceCaller(
                isTenant, m.Property?.OwnerUserId == callerUserId, m.Property?.BrokerUserId == callerUserId);

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
                NextActionLabel = MaintenanceStages.NextActionLabel(stage, date, m.Rating is not null, language),
                CreatedAt = m.CreatedAt,
                PropertyId = m.PropertyId,
                PropertyTitle = m.Property?.Title ?? string.Empty,
                OwnerName = m.Property?.OwnerName,
                VisitSummary = date is { } d && slot is { } s
                    ? $"{d.ToString("d MMMM yyyy", language.Culture)} · {MaintenanceTimeSlotNames.Label(s, language)}"
                    : null,
                VisitDate = date,
                VisitTime = m.ScheduledVisitTime,
                VisitTimePeriod = slot,
                TenantName = isTenant ? null : m.TenantName,
                Cost = m.QuoteTotal,
                RatingStars = m.Rating?.Stars,
                RatingAverage = m.Rating is null ? null : Average(
                    m.Rating.Stars, m.Rating.TechnicianRating, m.Rating.RepairQualityRating,
                    m.Rating.ResponseSpeedRating, m.Rating.BrokerCommunicationRating),
                AvailableActions = MaintenanceStages.Available(facts, caller),
                AssignedTechnician = m.Technician is null ? null : new TechnicianDto
                {
                    Id = m.Technician.Id,
                    Name = m.Technician.User?.FullName ?? string.Empty,
                    Role = language.Pick(m.Technician.RoleAr, m.Technician.RoleEn),
                    ImageUrl = m.Technician.User?.ProfileImageUrl,
                    AverageRating = m.Technician.RatingAverage,
                    PhoneNumber = m.Technician.User is null ? null : m.Technician.User.CountryCode + m.Technician.User.PhoneNumber,
                    Specialty = Specialty(m.Technician.Specialties.Select(s => (s.NameAr, s.NameEn, s.SortOrder)), language)
                }
            };
        }).ToList();
    }

    // "سباكة، كهرباء" / "Plumbing, Electrical". Shared with the details screen.
    public static string? Specialty(IEnumerable<(string NameAr, string NameEn, int SortOrder)> types, IRequestLanguage language)
    {
        var names = types.OrderBy(t => t.SortOrder).Select(t => language.Pick(t.NameAr, t.NameEn)).ToList();
        return names.Count == 0 ? null : string.Join(language.Pick("، ", ", "), names);
    }

    // Same rule as the rating on the details screen: the four criteria when given, else stars.
    private static decimal Average(int stars, params int?[] criteria)
    {
        var given = criteria.Where(c => c is not null).Select(c => c!.Value).ToList();
        return given.Count > 0 ? Math.Round((decimal)given.Sum() / given.Count, 1) : stars;
    }
}
