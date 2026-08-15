using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.Services;

public interface IComplaintService
{
    Task<ComplaintDto> CreateComplaintAsync(string studentId, CreateComplaintDto dto);
    Task<ComplaintDto?> GetByIdAsync(int id);
    Task<IEnumerable<ComplaintDto>> GetByStudentAsync(string studentId);
    Task<IEnumerable<ComplaintDto>> GetAllAsync();
    Task<IEnumerable<ComplaintDto>> GetAssignedComplaintsAsync(string staffUserId);
    Task<ComplaintDto> UpdateStatusAsync(int id, ComplaintStatus newStatus);
    Task<CursorResult<ComplaintDto>> GetByStudentPagedAsync(string studentId, string? cursor, int pageSize = 20, bool moveForward = true);
    Task<CursorResult<ComplaintDto>> GetFilteredPagedAsync(int? categoryId, ComplaintStatus? status, bool unreadOnly, string? currentUserId, string? staffScopeUserId, DateTime? startDate, DateTime? endDate, string? cursor, int pageSize = 20, bool moveForward = true);
    Task<CursorResult<ComplaintDto>> GetAllPagedAsync(string? cursor, int pageSize = 20, bool moveForward = true);
    Task<CursorResult<ComplaintDto>> GetAssignedComplaintsPagedAsync(string staffUserId, string? cursor, int pageSize = 20, bool moveForward = true);
    Task<List<CampaignSummaryDto>> GetCampaignSummariesAsync(string? staffScopeUserId);
}
