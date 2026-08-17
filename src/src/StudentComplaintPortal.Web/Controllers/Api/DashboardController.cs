using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Controllers.Api;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userRole = User.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userRole))
            return Unauthorized();

        var stats = await _dashboardService.GetDashboardStatsAsync(userId, userRole);
        return Ok(stats);
    }

    [HttpGet("pending-actions")]
    public async Task<ActionResult<PagedResult<PendingActionDto>>> GetPendingActions(int page = 1, int pageSize = 10)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userRole = User.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userRole))
            return Unauthorized();

        var result = await _dashboardService.GetPendingActionsAsync(userId, userRole, page, pageSize);
        return Ok(result);
    }

    [HttpGet("recent-activity")]
    public async Task<ActionResult<CursorResult<ActivityLogDto>>> GetRecentActivity(string? cursor = null, int pageSize = 15, bool forward = true)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userRole = User.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userRole))
            return Unauthorized();

        var result = await _dashboardService.GetRecentActivityAsync(userId, userRole, cursor, pageSize, forward);
        return Ok(result);
    }
}
