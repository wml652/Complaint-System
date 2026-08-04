using System.ComponentModel.DataAnnotations;

namespace StudentComplaintPortal.Application.DTOs;

public class UpdateCategoryDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

<<<<<<< HEAD:src/StudentComplaintPortal.Application/DTOs/CreateCategoryDto.cs
    public string? Icon { get; set; } = "📋";

    public string? Color { get; set; } = "#007bff";

=======
>>>>>>> ca47863b5b46c56bcce3b302b35d7ad96e295654:src/StudentComplaintPortal.Application/DTOs/UpdateCategoryDto.cs
    public List<string> AssigneeIds { get; set; } = new();

    public List<CategoryAttachmentRuleDto> AttachmentRules { get; set; } = new();
}
