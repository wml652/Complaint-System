using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Domain.Entities;

public class Complaint
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public ComplaintCategory Category { get; set; }
    public ComplaintStatus Status { get; set; }
    public required string StudentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }

    // New dynamic category (Categories table) - separate from the legacy ComplaintCategory enum above.
    // Named AssignedCategory to avoid clashing with the existing 'Category' enum property.
    public int? CategoryId { get; set; }

    // Navigation properties
    public AppUser Student { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public Category? AssignedCategory { get; set; }
}