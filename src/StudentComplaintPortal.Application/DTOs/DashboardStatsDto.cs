namespace StudentComplaintPortal.Application.DTOs;

public class DashboardStatsDto
{
    public int TotalComplaints { get; set; }
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int ResolvedCount { get; set; }
    public int ClosedCount { get; set; }

    public List<ComplaintsByStatusDto> ComplaintsByStatus { get; set; } = new();
    public List<ComplaintsByCategoryDto> ComplaintsByCategory { get; set; } = new();
    public List<ComplaintsOverTimeDto> ComplaintsOverTime { get; set; } = new();
    public CursorResult<ActivityLogDto> RecentActivity { get; set; } = new();
    public PagedResult<PendingActionDto> PendingActions { get; set; } = new();
}

public class ComplaintsByStatusDto
{
    public required string Status { get; set; }
    public int Count { get; set; }
}

public class ComplaintsByCategoryDto
{
    public required string Category { get; set; }
    public int Count { get; set; }
    public required string Color { get; set; }
}

public class ComplaintsOverTimeDto
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class ActivityLogDto
{
    public int Id { get; set; }
    public required string Action { get; set; }
    public required string Description { get; set; }
    public DateTime Timestamp { get; set; }
    public required string InitiatedBy { get; set; }
}

public class PendingActionDto
{
    public int ComplaintId { get; set; }
    public required string Title { get; set; }
    public required string StudentName { get; set; }
    public required string Status { get; set; }
    public required string Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public int DaysPending { get; set; }
}

