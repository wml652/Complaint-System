using StudentComplaintPortal.Application.DTOs;

namespace StudentComplaintPortal.Application.Services;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(string userId, string userRole);
    Task<PagedResult<PendingActionDto>> GetPendingActionsAsync(string userId, string userRole, int pageNumber, int pageSize = 10);
    Task<CursorResult<ActivityLogDto>> GetRecentActivityAsync(string userId, string userRole, string? cursor, int pageSize = 15, bool moveForward = true);
}
