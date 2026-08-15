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
    private readonly ICategoryService _categoryService;

    public DashboardController(IComplaintService complaintService, ICategoryService categoryService)
    {
        _complaintService = complaintService;
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string status = "All", int? categoryId = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
        {
            return Forbid();
        }

        if (User.IsInRole("Student"))
        {
            var complaints = await _complaintService.GetByStudentAsync(userId);
            return View("StudentIndex", complaints);
        }
        else if (User.IsInRole("Admin"))
        {
            var complaints = await _complaintService.GetAllAsync();
            return View("AdminIndex", complaints);
        }
        else if (User.IsInRole("Staff"))
        {
            //if staff explicitly has the permission "view all" then give admin view 
            if (User.HasClaim("Permission", "Complaints.ViewAll"))
            {
                var allComplaints = (await _complaintService.GetAllAsync()).ToList();
                var adminStyleViewModel = new AdminDashboardViewModel
                {
                    Complaints = allComplaints,
                    TotalCount = allComplaints.Count,
                    PendingCount = allComplaints.Count(c => c.Status == "Open"),
                    InProgressCount = allComplaints.Count(c => c.Status == "InProgress"),
                    ResolvedCount = allComplaints.Count(c => c.Status == "Resolved"),
                    SelectedStatus = status
                };
                return View("AdminIndex", adminStyleViewModel);
            }

            var categories = (await _categoryService.GetCategoriesForStaffAsync(userId)).ToList();
            var allAssigned = (await _complaintService.GetAssignedComplaintsAsync(userId)).ToList();

            int? selectedCategoryId = null;
            string selectedCategoryName = "My";
            List<ComplaintDto> scopedComplaints = new();

            if (categories.Count > 0)
            {
                // Step 1: session mein pehle se koi selection saved hai?
                int? rememberedCategoryId = HttpContext.Session.GetInt32("StaffCategoryId");

                // Step 2: agar dropdown se abhi nayi category select hui hai, wo priority lega
                if (categoryId != null)
                {
                    rememberedCategoryId = categoryId;
                }

                // Step 3: wo category dhoondo, na mile to pehli le lo
                var selectedCategory = categories.FirstOrDefault(c => c.Id == rememberedCategoryId) ?? categories[0];

                // Step 4: save kar do taake agle page pe bhi yaad rahe
                HttpContext.Session.SetInt32("StaffCategoryId", selectedCategory.Id);

                selectedCategoryId = selectedCategory.Id;
                selectedCategoryName = selectedCategory.Name;
                scopedComplaints = allAssigned.Where(c => c.Category == selectedCategoryName).ToList();
            }

            var viewModel = new StaffDashboardViewModel
            {
                Complaints = scopedComplaints,
                TotalCount = scopedComplaints.Count,
                PendingCount = scopedComplaints.Count(c => c.Status == "Open"),
                InProgressCount = scopedComplaints.Count(c => c.Status == "InProgress"),
                ResolvedCount = scopedComplaints.Count(c => c.Status == "Resolved"),
                SelectedStatus = status,
                AssignedCategories = categories,
                SelectedCategoryId = selectedCategoryId,
                SelectedCategoryName = selectedCategoryName
            };

            return View("StaffIndex", viewModel);
        }

        return Forbid();
    }

    [HttpGet]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> NewComplaint()
    {
        // Fetch active categories from database
        var categories = await _categoryService.GetActiveCategoriesForDropdownAsync();
        ViewBag.Categories = categories;

        return View();
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult CategoryManagement()
    {
        return View();
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Staff")]
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
