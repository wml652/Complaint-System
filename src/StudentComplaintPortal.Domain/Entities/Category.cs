namespace StudentComplaintPortal.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; } = "📋"; // Emoji icon for visual representation
    public string? Color { get; set; } = "#007bff"; // Color hex code for category badge
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<CategoryAttachmentRule> AttachmentRules { get; set; } = new List<CategoryAttachmentRule>();
    public ICollection<CategoryAssignee> Assignees { get; set; } = new List<CategoryAssignee>();
}
