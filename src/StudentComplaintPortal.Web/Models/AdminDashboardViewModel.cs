using StudentComplaintPortal.Application.DTOs;

namespace StudentComplaintPortal.Web.Models;

// View-layer only model for the Admin dashboard (wireframe: stat cards + filters).
// Built from ComplaintDto data the controller already fetches - no changes
// to Application/Data layers required for this part.
public class AdminDashboardViewModel
{
    public IEnumerable<ComplaintDto> Complaints { get; set; } = new List<ComplaintDto>();

    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int InProgressCount { get; set; }
    public int ResolvedCount { get; set; }

    public string SelectedStatus { get; set; } = "All";
    public string SelectedCategory { get; set; } = "All";
}
