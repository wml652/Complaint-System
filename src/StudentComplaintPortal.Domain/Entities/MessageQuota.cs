namespace StudentComplaintPortal.Domain.Entities;

public class MessageQuota
{
    public int Id { get; set; }
    public int ComplaintId { get; set; }
    public required string StudentId { get; set; }
    public int MessagesRemaining { get; set; }
    public DateTime? LastStaffMessageAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Complaint Complaint { get; set; } = null!;
    public AppUser Student { get; set; } = null!;

    // Constants
    public const int MAX_MESSAGES_PER_STAFF_RESPONSE = 10;
}
