namespace StudentComplaintPortal.Domain.Entities;

/// <summary>
/// Catalog of permissions available in the system. Stored in DB so new
/// permissions can be added without changing code (dynamic/modular design).
/// </summary>
public class Permission
{
    public int Id { get; set; }

    /// <summary>Unique code used as the Claim value, e.g. "Complaints.ViewAll"</summary>
    public required string Code { get; set; }

    /// <summary>Human-readable label shown as checkbox text, e.g. "View all complaints"</summary>
    public required string DisplayName { get; set; }

    /// <summary>Groups permissions in the UI, e.g. "Complaints", "Users"</summary>
    public required string Module { get; set; }

    public string? Description { get; set; }
}
