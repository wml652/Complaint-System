using StudentComplaintPortal.Application.DTOs;

namespace StudentComplaintPortal.Web.Models;

// View-layer model for the Staff dashboard. Unlike the Admin dashboard,
// stats are always scoped to ONE category at a time (never combined),
// with a dropdown to switch categories when a staff member has more than one.
public class StaffDashboardViewModel
{
    public IEnumerable<ComplaintDto> Complaints { get; set; } = new List<ComplaintDto>();

    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int InProgressCount { get; set; }
    public int ResolvedCount { get; set; }

    public string SelectedStatus { get; set; } = "All";

    // All categories this staff member is assigned to (for the switcher dropdown).
    public List<CategoryDto> AssignedCategories { get; set; } = new();

    public int? SelectedCategoryId { get; set; }
    public string SelectedCategoryName { get; set; } = string.Empty;
}