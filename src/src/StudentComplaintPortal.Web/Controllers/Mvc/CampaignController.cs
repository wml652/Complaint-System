using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentComplaintPortal.Application.Services;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Controllers.Mvc;

// Semester-wise complaint overview. Naya saal aane pe naya group khud ban jata hai
// (Complaint.CreatedAt se calculate hota hai) - is controller ko cherne ki zaroorat nahi.
[Authorize(Roles = "Admin,Staff")]
public class CampaignController : Controller
{
    private readonly IComplaintService _complaintService;

    public CampaignController(IComplaintService complaintService)
    {
        _complaintService = complaintService;
    }

    [HttpGet]
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdminOrViewAll = User.IsInRole("Admin") || User.HasClaim("Permission", "Complaints.ViewAll");

        var summaries = await _complaintService.GetCampaignSummariesAsync(isAdminOrViewAll ? null : userId);
        return View(summaries);
    }
}