using Microsoft.AspNetCore.Identity;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Domain.Entities;

public class AppUser : IdentityUser
{
    public required string FullName { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string? QueryAlias { get; set; }

    // Navigation properties
    public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
