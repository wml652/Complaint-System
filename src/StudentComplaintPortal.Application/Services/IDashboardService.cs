using StudentComplaintPortal.Application.DTOs;

namespace StudentComplaintPortal.Application.Services;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(string userId, string userRole);
}
