using System.ComponentModel.DataAnnotations;

namespace StudentComplaintPortal.Application.DTOs;

public class CreateCategoryDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public string? Color { get; set; }

    public List<string> AssigneeIds { get; set; } = new();

    public List<CategoryAttachmentRuleDto> AttachmentRules { get; set; } = new();
}
