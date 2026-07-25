namespace StudentComplaintPortal.Application.DTOs;

public class CreateCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CategoryAttachmentRuleDto> AttachmentRules { get; set; } = new();
    public List<string> AssigneeIds { get; set; } = new();
}
