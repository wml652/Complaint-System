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
}
