using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data.Repositories;

public class MessageQuotaRepository : GenericRepository<MessageQuota>, IMessageQuotaRepository
{
    public MessageQuotaRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<MessageQuota?> GetAsync(int complaintId, string studentId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(q => q.ComplaintId == complaintId && q.StudentId == studentId);
    }

    public async Task<List<MessageQuota>> GetAllForComplaintAsync(int complaintId)
    {
        return await _dbSet
            .Where(q => q.ComplaintId == complaintId)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(int complaintId, string studentId)
    {
        return await _dbSet
            .AnyAsync(q => q.ComplaintId == complaintId && q.StudentId == studentId);
    }
}