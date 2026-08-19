namespace StudentComplaintPortal.Application.DTOs;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; }
    public List<CategoryAttachmentRuleDto> AttachmentRules { get; set; } = new();
    public List<string> AssigneeIds { get; set; } = new();
}
