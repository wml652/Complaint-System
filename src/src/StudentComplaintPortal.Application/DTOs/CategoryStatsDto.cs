namespace StudentComplaintPortal.Application.DTOs;

public class CategoryStatsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalComplaints { get; set; }
    public int OpenComplaints { get; set; }
    public int ResolvedComplaints { get; set; }
    public List<StaffAssigneeDto> AssignedStaff { get; set; } = new();
}
