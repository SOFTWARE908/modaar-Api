using System.Text.Json.Serialization;
using modaar.api.Features.Contracts.Entities;
using modaar.api.Features.Users.Entities;
using PropertyEntity = modaar.api.Features.Properties.Entities.Property;

namespace modaar.api.Features.Agreements.Entities;

// تفويض: an owner hands some of their units to a broker to run. There is no end date to type
// in: each unit leaves when its own lease ends (see AgreementProperty), and the agreement
// expires when its last unit has left.
//
// Property.BrokerId stays the "who runs this unit now" the rest of the system reads; only this
// feature writes it — set when an agreement goes active, cleared when the unit leaves.
public class ManagementAgreement
{
    public Guid Id { get; set; }

    // MD-2026-000123. Always ours; never typed in.
    public string Number { get; set; } = null!;

    public Guid BrokerId { get; set; }
    public Guid OwnerUserId { get; set; }

    // Navigation, never lazy-loaded: Include it or project through it.
    public Broker Broker { get; set; } = null!;
    public User Owner { get; set; } = null!;

    public ManagementAgreementStatus Status { get; set; } = ManagementAgreementStatus.PendingOwner;

    // Terms. ManagementFeePercent is null only on agreements created from broker links that
    // existed before agreements did.
    public decimal? ManagementFeePercent { get; set; }
    public LeasingFeeType? LeasingFeeType { get; set; }
    public decimal? LeasingFeeValue { get; set; }
    public string? DocumentUrl { get; set; }

    // The owner's request this answered, if it started from one.
    public Guid? RequestId { get; set; }

    // The agreement this one renews. Approving it moves the old one's units across.
    public Guid? RenewedFromId { get; set; }

    public Guid CreatedByUserId { get; set; }

    // Set when the owner approves.
    public DateOnly? StartDate { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    // Last day it covered anything: expiry, termination, rejection or withdrawal.
    public DateOnly? EndedOn { get; set; }
    public Guid? EndedByUserId { get; set; }
    public string? EndReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<AgreementProperty> Properties { get; set; } = [];
}

// A unit inside an agreement. Rows are written when the agreement is created (the proposal) and
// stay after the unit leaves, as history.
public class AgreementProperty
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity Property { get; set; } = null!;

    // Set when the agreement goes active. A unit is "held" while ActiveFrom is set and ExitedOn
    // isn't; a unique index keeps a unit in at most one held row.
    public DateOnly? ActiveFrom { get; set; }

    // The lease the unit leaves with, and when. Both null while the unit is vacant: it stays
    // until the first lease signed on it ends.
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public DateOnly? EndsOn { get; set; }

    public DateOnly? ExitedOn { get; set; }
    public AgreementUnitExitReason? ExitReason { get; set; }
}

// What happened to an agreement, for its timeline.
public class AgreementEvent
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    public AgreementEventType Type { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? PropertyId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset At { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ManagementAgreementStatus
{
    // Terms written by the broker, waiting for the owner.
    PendingOwner,
    Active,
    // Every unit has left (or a renewal replaced it).
    Expired,
    // Cancelled by either side while active.
    Terminated,
    // The owner said no.
    Rejected,
    // The broker took it back before the owner answered.
    Withdrawn
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeasingFeeType
{
    // Percent of the annual rent.
    Percent,
    Fixed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgreementUnitExitReason
{
    LeaseEnded,
    LeaseTerminated,
    // The next lease on the unit was made by someone else.
    LeasedByOtherBroker,
    AgreementTerminated,
    // Moved to the agreement that renewed this one.
    Renewed,
    // The unit was deleted or changed owner.
    Removed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgreementEventType
{
    Created,
    Approved,
    Rejected,
    Withdrawn,
    Terminated,
    UnitExited,
    UnitAdded,
    Renewed,
    Expired
}
