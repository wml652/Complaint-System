using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data.Repositories;

public interface IMessageRepository : IGenericRepository<Message>
{
    Task<IEnumerable<Message>> GetByComplaintIdAsync(int complaintId);
    Task<List<Message>> GetByComplaintIdPagedAsync(int complaintId, int? cursorId, int pageSize, bool moveForward = true);
    Task<List<int>> GetUnreadMessageIdsAsync(int complaintId, string userId);
}