namespace modaar.api.Features.Maintenance.Entities;

// An owner's private technician: who the owner picks from on the units they run themselves
// (selfManaged, or no broker). One technician can serve several owners. Only the admin links
// and unlinks them.
//
// A technician linked to any owner and not to a brokerage is private, not independent: brokers
// don't see or assign them.
public class OwnerTechnician
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid TechnicianId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
