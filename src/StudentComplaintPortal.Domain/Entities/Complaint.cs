using StudentComplaintPortal.Domain.Enums;
namespace StudentComplaintPortal.Domain.Entities;
public class Complaint
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public ComplaintCategory Category { get; set; }  // Keep for backward compatibility
    public ComplaintStatus Status { get; set; }
    public required string StudentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
    // NEW: Link to Category table for staff assignment
    public int? CategoryId { get; set; }  // Nullable for backward compatibility
    public Category? CategoryEntity { get; set; }  // Navigation property
    // Navigation properties
    public AppUser Student { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}