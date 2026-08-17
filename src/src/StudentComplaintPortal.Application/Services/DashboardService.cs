using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.ServiceHelper;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace StudentComplaintPortal.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;

    public DashboardService(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(string userId, string userRole)
    {
        var stats = new DashboardStatsDto();

        // Get all complaints or filtered by user role
        var complaints = await _unitOfWork.Complaints.GetAllAsync();

        if (userRole == "Student")
        {
            complaints = complaints.Where(c => c.StudentId == userId).ToList();
        }

        // Calculate status counts
        stats.TotalComplaints = complaints.Count();
        stats.OpenCount = complaints.Count(c => c.Status == ComplaintStatus.Open);
        stats.InProgressCount = complaints.Count(c => c.Status == ComplaintStatus.InProgress);
        stats.ResolvedCount = complaints.Count(c => c.Status == ComplaintStatus.Resolved);
        stats.ClosedCount = complaints.Count(c => c.Status == ComplaintStatus.Closed);

        // Build complaints by status
        stats.ComplaintsByStatus = new List<ComplaintsByStatusDto>
        {
            new() { Status = "Open", Count = stats.OpenCount },
            new() { Status = "In Progress", Count = stats.InProgressCount },
            new() { Status = "Resolved", Count = stats.ResolvedCount },
            new() { Status = "Closed", Count = stats.ClosedCount }
        };

        // Build complaints by category
        var categoryColors = new Dictionary<string, string>
        {
            { "Academic", "#0d6efd" },
            { "Hostel", "#198754" },
            { "Administrative", "#6f42c1" },
            { "Other", "#6c757d" }
        };

        stats.ComplaintsByCategory = complaints
            .GroupBy(c => c.Category.ToString())
            .Select(g => new ComplaintsByCategoryDto
            {
                Category = g.Key,
                Count = g.Count(),
                Color = categoryColors.ContainsKey(g.Key) ? categoryColors[g.Key] : "#6c757d"
            })
            .ToList();

        // Build complaints over time (last 30 days)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        stats.ComplaintsOverTime = complaints
            .Where(c => c.CreatedAt >= thirtyDaysAgo)
            .GroupBy(c => c.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new ComplaintsOverTimeDto
            {
                Date = g.Key,
                Count = g.Count()
            })
            .ToList();

        // Build recent activity (first page)
        stats.RecentActivity = await GetRecentActivityAsync(userId, userRole, cursor: null);

        // Build pending actions (first page)
        stats.PendingActions = await GetPendingActionsAsync(userId, userRole, pageNumber: 1);

        return stats;
    }

    public async Task<PagedResult<PendingActionDto>> GetPendingActionsAsync(string userId, string userRole, int pageNumber, int pageSize = 10)
    {
        var complaints = await _unitOfWork.Complaints.GetAllAsync();

        if (userRole == "Student")
        {
            complaints = complaints.Where(c => c.StudentId == userId).ToList();
        }

        var pendingComplaints = complaints
            .Where(c => c.Status == ComplaintStatus.Open || c.Status == ComplaintStatus.InProgress)
            .ToList();

        var pendingActions = await BuildPendingActionsAsync(pendingComplaints);

        return PaginationHelper.PaginateByPage(pendingActions, pageNumber, pageSize);
    }

    public async Task<CursorResult<ActivityLogDto>> GetRecentActivityAsync(string userId, string userRole, string? cursor, int pageSize = 15, bool moveForward = true)
    {
        var complaints = await _unitOfWork.Complaints.GetAllAsync();

        if (userRole == "Student")
        {
            complaints = complaints.Where(c => c.StudentId == userId).ToList();
        }

        var activities = await BuildAllActivitiesAsync(complaints.ToList());

        return PaginationHelper.PaginateByCursorTimestamp(activities, a => a.Timestamp, cursor, pageSize, moveForward);
    }

    private async Task<List<ActivityLogDto>> BuildAllActivitiesAsync(List<Complaint> complaints)
    {
        var activities = new List<ActivityLogDto>();

        foreach (var complaint in complaints)
        {
            var student = await _userManager.FindByIdAsync(complaint.StudentId);
            activities.Add(new ActivityLogDto
            {
                Id = complaint.Id,
                Action = "Complaint Created",
                Description = $"Student filed: \"{complaint.Title}\"",
                Timestamp = complaint.CreatedAt,
                InitiatedBy = student?.FullName ?? "Unknown"
            });

            if (complaint.UpdatedAt > complaint.CreatedAt.AddSeconds(1))
            {
                activities.Add(new ActivityLogDto
                {
                    Id = complaint.Id,
                    Action = "Status Changed",
                    Description = $"Status updated to: {complaint.Status}",
                    Timestamp = complaint.UpdatedAt,
                    InitiatedBy = "Staff"
                });
            }
        }

        return activities.OrderByDescending(a => a.Timestamp).ToList();
    }

    private async Task<List<PendingActionDto>> BuildPendingActionsAsync(List<Complaint> complaints)
    {
        var pendingActions = new List<PendingActionDto>();

        foreach (var complaint in complaints)
        {
            var student = await _userManager.FindByIdAsync(complaint.StudentId);
            var daysPending = (int)(DateTime.UtcNow - complaint.CreatedAt).TotalDays;

            pendingActions.Add(new PendingActionDto
            {
                ComplaintId = complaint.Id,
                Title = complaint.Title,
                StudentName = student?.FullName ?? "Unknown",
                Status = complaint.Status.ToString(),
                Category = complaint.Category.ToString(),
                CreatedAt = complaint.CreatedAt,
                DaysPending = daysPending
            });
        }

        return pendingActions.OrderByDescending(p => p.DaysPending).ToList();
    }
}
