using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;
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
    public async Task<IActionResult> Index()
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
            var complaints = await _complaintService.GetAllAsync();
            return View("AdminIndex", complaints);
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
}
