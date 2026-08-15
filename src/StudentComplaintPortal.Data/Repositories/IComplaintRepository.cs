using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;
namespace StudentComplaintPortal.Data.Repositories;
public interface IComplaintRepository : IGenericRepository<Complaint>
{
    Task<IEnumerable<Complaint>> GetByStudentIdAsync(string studentId);
    Task<Complaint?> GetByIdWithMessagesAsync(int id);
    Task<List<Complaint>> GetByStudentIdPagedAsync(string studentId, DateTime? cursorTimestamp, int pageSize, bool moveForward = true);
    Task<List<Complaint>> GetAllPagedAsync(DateTime? cursorTimestamp, int pageSize, bool moveForward = true);
    Task<List<Complaint>> GetAssignedToStaffPagedAsync(string staffUserId, DateTime? cursorTimestamp, int pageSize, bool moveForward = true);
    Task<List<Complaint>> GetFilteredPagedAsync(int? categoryId, ComplaintStatus? status, bool unreadOnly, string? currentUserId, string? staffScopeUserId, DateTime? startDate, DateTime? endDate, DateTime? cursorTimestamp, int pageSize, bool moveForward = true);
    // Complaints whose category has the given staff member as an assignee
    Task<IEnumerable<Complaint>> GetAssignedToStaffAsync(string staffUserId);
}