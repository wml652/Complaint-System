using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data.Repositories;

public interface IMessageQuotaRepository : IGenericRepository<MessageQuota>
{
    Task<MessageQuota?> GetAsync(int complaintId, string studentId);
    Task<List<MessageQuota>> GetAllForComplaintAsync(int complaintId);
    Task<bool> ExistsAsync(int complaintId, string studentId);
}