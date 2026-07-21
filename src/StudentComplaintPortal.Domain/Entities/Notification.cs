namespace StudentComplaintPortal.Domain.Entities;

public class Notification
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string Message { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public AppUser User { get; set; } = null!;
}
