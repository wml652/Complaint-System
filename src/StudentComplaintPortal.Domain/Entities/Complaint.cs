using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Domain.Entities;

public class Complaint
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public int CategoryId { get; set; }
    public ComplaintStatus Status { get; set; }
    public Priority? Priority { get; set; }
    public required string StudentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Category Category { get; set; } = null!;
    public AppUser Student { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
