using modaar.api.Features.Users.Enums;


// An owner asking a brokerage to manage their properties. The only thing in the system that
// could ever set Property.BrokerId — today nothing else assigns a broker.
//
// Deliberately records the ask only. Nothing here approves it: the spec has no decision
// endpoint, and the assignment is expected to come from whatever system uploads the
// portfolio data.
public class BrokerManagementRequest
{
    public Guid Id { get; set; }

    public Guid BrokerId { get; set; }
    public Guid OwnerUserId { get; set; }

    // Empty means the whole portfolio. Stored as JSON: read and written whole, never joined
    // against.
    public List<Guid> PropertyIds { get; set; } = [];

    public BrokerManagementRequestStatus Status { get; set; } = BrokerManagementRequestStatus.Pending;

    public Guid? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
