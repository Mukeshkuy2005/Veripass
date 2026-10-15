using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class Director : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public string Designation { get; set; } = "Director";
    public string? Din { get; set; } // Director Identification Number (8 digits)
    public string? PanMasked { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public string Status { get; set; } = "ACTIVE";
}
