using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data.Repositories;

public class MessageRepository : GenericRepository<Message>, IMessageRepository
{
    public MessageRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Message>> GetByComplaintIdAsync(int complaintId)
    {
        return await _dbSet
            .Where(m => m.ComplaintId == complaintId)
            .Include(m => m.Sender)
            .Include(m => m.Attachments)
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }

    public async Task<List<Message>> GetByComplaintIdPagedAsync(int complaintId, int? cursorId, int pageSize, bool moveForward = true)
    {
        var query = _dbSet
            .Where(m => m.ComplaintId == complaintId)
            .Include(m => m.Sender)
            .Include(m => m.Attachments)
            .AsQueryable();

        if (cursorId.HasValue)
        {
            query = moveForward
                ? query.Where(m => m.Id < cursorId.Value)
                : query.Where(m => m.Id > cursorId.Value);
        }

        return await query
            .OrderByDescending(m => m.Id)
            .Take(pageSize + 1)
            .ToListAsync();
    }
    public async Task<List<int>> GetUnreadMessageIdsAsync(int complaintId, string userId)
    {
        return await _dbSet
            .Where(m => m.ComplaintId == complaintId
                     && m.SenderId != userId
                     && !m.ReadAt.HasValue)
            .Select(m => m.Id)
            .ToListAsync();
    }
}
