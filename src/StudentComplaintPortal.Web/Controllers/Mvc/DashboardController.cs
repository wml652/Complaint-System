using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Web.Models;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Controllers.Mvc;

[Authorize]
public class DashboardController : Controller
{
    private readonly IComplaintService _complaintService;

    public DashboardController(IComplaintService complaintService)
    {
        _complaintService = complaintService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string status = "All")
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
        {
            return Forbid();
        }

        if (role == "Student")
        {
            var complaints = await _complaintService.GetByStudentAsync(userId);
            return View("StudentIndex", complaints);
        }
        else if (role == "Admin")
        {
            var complaints = (await _complaintService.GetAllAsync()).ToList();

            // Wireframe stat cards. "Pending" in the wireframe maps to our
            // ComplaintStatus.Open value - no new status is being introduced.
            var viewModel = new AdminDashboardViewModel
            {
                Complaints = complaints,
                TotalCount = complaints.Count,
                PendingCount = complaints.Count(c => c.Status == "Open"),
                InProgressCount = complaints.Count(c => c.Status == "InProgress"),
                ResolvedCount = complaints.Count(c => c.Status == "Resolved"),
                SelectedStatus = status
            };

            return View("AdminIndex", viewModel);
        }

        return Forbid();
    }

    [HttpGet]
    [Authorize(Roles = "Student")]
    public IActionResult NewComplaint()
    {
        return View();
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewComplaint(CreateComplaintDto model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Forbid();
        }

        var complaint = await _complaintService.CreateComplaintAsync(userId, model);
        return RedirectToAction("Detail", "Complaint", new { id = complaint.Id });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult CategoryManagement()
    {
        return View();
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MyAssignedComplaints()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Forbid();
        }

        var complaints = await _complaintService.GetAssignedComplaintsAsync(userId);
        return View(complaints);
    }
}
