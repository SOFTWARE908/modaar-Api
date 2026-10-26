using FluentValidation;

namespace modaar.api.Features.Maintenance.Dtos;

// Admin: link a technician to an owner by phone. An existing technician with that number is
// reused (and can serve several owners); otherwise one is created.
public record AddOwnerTechnicianDto
{
    public string CountryCode { get; init; } = "+966";
    public string PhoneNumber { get; init; } = string.Empty;

    // Required only when the number has no account yet.
    public string? FullName { get; init; }

    // Job title under the name ("سبّاك" / "Plumber"). Required only for a new technician.
    public string? RoleAr { get; init; }
    public string? RoleEn { get; init; }

    // Maintenance service types they take. Replaces the technician's list when sent.
    public IReadOnlyList<Guid>? ServiceTypeIds { get; init; }
}

public record OwnerTechnicianDto
{
    public required Guid TechnicianId { get; init; }
    public required string Name { get; init; }
    public required string PhoneNumber { get; init; }
    public string? AvatarUrl { get; init; }
    public required string Role { get; init; }
    public required IReadOnlyList<string> Specialties { get; init; }
    public required bool IsAvailable { get; init; }

    // Also works for a brokerage (then that brokerage can assign them too).
    public required bool HasBroker { get; init; }

    // How many owners this technician is linked to.
    public required int OwnersCount { get; init; }

    public required DateTimeOffset LinkedAt { get; init; }
}

public sealed class AddOwnerTechnicianValidator : AbstractValidator<AddOwnerTechnicianDto>
{
    public AddOwnerTechnicianValidator()
    {
        RuleFor(x => x.CountryCode).NotEmpty().Matches(@"^\+?\d{1,4}$");
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^\d{6,14}$").WithMessage("phoneNumber is digits only.");
        RuleFor(x => x.FullName).MaximumLength(200);
        RuleFor(x => x.RoleAr).MaximumLength(100);
        RuleFor(x => x.RoleEn).MaximumLength(100);
        RuleFor(x => x.ServiceTypeIds!.Count).LessThanOrEqualTo(20).When(x => x.ServiceTypeIds is not null);
    }
}
