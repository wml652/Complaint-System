using StudentComplaintPortal.Application.DTOs;

namespace StudentComplaintPortal.Web.Models;

public class AdminDashboardViewModel
{
    public IEnumerable<ComplaintDto> Complaints { get; set; } = new List<ComplaintDto>();

    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int InProgressCount { get; set; }
    public int ResolvedCount { get; set; }

    public string SelectedStatus { get; set; } = "All";
}
