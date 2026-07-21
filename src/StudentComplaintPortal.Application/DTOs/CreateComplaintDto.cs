using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.DTOs;

public class CreateComplaintDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComplaintCategory Category { get; set; }
}
