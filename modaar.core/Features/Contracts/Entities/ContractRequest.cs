using modaar.api.Features.Contracts.Enums;

namespace modaar.api.Features.Contracts.Entities;

// A tenant's request against their lease — renew it or end it — and how it was answered.
// Backs ContractDetailsDto.requestsHistory and the pending row that POST /renew returns.
public class ContractRequest
{
    public Guid Id { get; set; }

    public Guid ContractId { get; set; }

    public ContractRequestType Type { get; set; }
    public ContractRequestStatus Status { get; set; } = ContractRequestStatus.Pending;

    public Guid RequestedByUserId { get; set; }
    public string? Notes { get; set; }

    // Null while pending. DecidedByUserId is the owner or broker who answered.
    public Guid? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }

    // Set when an accepted renewal produces a new lease, so the history can link to it.
    public Guid? ResultingContractId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
