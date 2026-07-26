using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.Services;

public interface IComplaintService
{
    Task<ComplaintDto> CreateComplaintAsync(string studentId, CreateComplaintDto dto);
    Task<ComplaintDto?> GetByIdAsync(int id);
    Task<IEnumerable<ComplaintDto>> GetByStudentAsync(string studentId);
    Task<IEnumerable<ComplaintDto>> GetAllAsync();
    Task<ComplaintDto> UpdateStatusAsync(int id, ComplaintStatus newStatus);
    Task<IEnumerable<ComplaintDto>> GetAssignedComplaintsAsync(string staffUserId);
}
