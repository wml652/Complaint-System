using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data.Repositories;

public class ComplaintRepository : GenericRepository<Complaint>, IComplaintRepository
{
    public ComplaintRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Complaint>> GetByStudentIdAsync(string studentId)
    {
        return await _dbSet
            .Where(c => c.StudentId == studentId)
            .Include(c => c.Student)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Complaint?> GetByIdWithMessagesAsync(int id)
    {
        return await _dbSet
            .Include(c => c.Student)
            .Include(c => c.Messages)
                .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Complaint>> GetByStudentIdPagedAsync(string studentId, DateTime? cursorTimestamp, int pageSize, bool moveForward = true)
    {
        var query = _dbSet.Where(c => c.StudentId == studentId).Include(c => c.Student).AsQueryable();

        if (cursorTimestamp.HasValue)
        {
            query = moveForward
                ? query.Where(c => (c.LastMessageAt ?? c.CreatedAt) < cursorTimestamp.Value)
                : query.Where(c => (c.LastMessageAt ?? c.CreatedAt) > cursorTimestamp.Value);
        }

        return await query.OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).Take(pageSize + 1).ToListAsync();
    }

    public async Task<List<Complaint>> GetAllPagedAsync(DateTime? cursorTimestamp, int pageSize, bool moveForward = true)
    {
        var query = _dbSet.Include(c => c.Student).AsQueryable();

        if (cursorTimestamp.HasValue)
        {
            query = moveForward
                ? query.Where(c => (c.LastMessageAt ?? c.CreatedAt) < cursorTimestamp.Value)
                : query.Where(c => (c.LastMessageAt ?? c.CreatedAt) > cursorTimestamp.Value);
        }

        return await query.OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).Take(pageSize + 1).ToListAsync();
    }

    public async Task<List<Complaint>> GetAssignedToStaffPagedAsync(string staffUserId, DateTime? cursorTimestamp, int pageSize, bool moveForward = true)
    {
        var query = _dbSet.Include(c => c.Student).AsQueryable();

        if (cursorTimestamp.HasValue)
        {
            query = moveForward
                ? query.Where(c => (c.LastMessageAt ?? c.CreatedAt) < cursorTimestamp.Value)
                : query.Where(c => (c.LastMessageAt ?? c.CreatedAt) > cursorTimestamp.Value);
        }

        return await query.OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).Take(pageSize + 1).ToListAsync();
    }

    public override async Task<Complaint?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(c => c.Student)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public override async Task<IEnumerable<Complaint>> GetAllAsync()
    {
        return await _dbSet
            .Include(c => c.Student)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Complaint>> GetAssignedToStaffAsync(string staffUserId)
    {
        // TODO: Implement proper category-based assignment once Category entity system is added
        // For now, return all complaints
        return await _dbSet
            .Include(c => c.Student)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }
}