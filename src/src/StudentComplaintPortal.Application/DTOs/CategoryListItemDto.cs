namespace StudentComplaintPortal.Application.DTOs;

public class CategoryListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; } = "📋";
    public string? Color { get; set; } = "#007bff";
    public bool IsActive { get; set; }
    public int ComplaintCount { get; set; }
    public int AssignedStaffCount { get; set; }
}
