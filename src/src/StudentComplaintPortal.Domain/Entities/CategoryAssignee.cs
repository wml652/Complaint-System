namespace StudentComplaintPortal.Domain.Entities;

public class CategoryAssignee
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public required string AppUserId { get; set; }

    // Navigation properties
    public Category Category { get; set; } = null!;
    public AppUser AppUser { get; set; } = null!;
}
