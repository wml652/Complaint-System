using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

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
        // The Fix: Added the Where clause to filter complaints by the staff member's assigned categories
        var query = _dbSet.Include(c => c.Student)
            .Where(c => _context.Set<CategoryAssignee>()
                .Any(ca => ca.CategoryId == c.CategoryId && ca.AppUserId == staffUserId))
            .AsQueryable();

        if (cursorTimestamp.HasValue)
        {
            query = moveForward
                ? query.Where(c => (c.LastMessageAt ?? c.CreatedAt) < cursorTimestamp.Value)
                : query.Where(c => (c.LastMessageAt ?? c.CreatedAt) > cursorTimestamp.Value);
        }

        return await query.OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).Take(pageSize + 1).ToListAsync();
    }

    public async Task<List<Complaint>> GetFilteredPagedAsync(int? categoryId, ComplaintStatus? status, bool unreadOnly, string? currentUserId, string? staffScopeUserId, DateTime? startDate, DateTime? endDate, DateTime? cursorTimestamp, int pageSize, bool moveForward = true)
    {
        var query = _dbSet.Include(c => c.Student).AsQueryable();

        if (!string.IsNullOrEmpty(staffScopeUserId))
        {
            var assignedCategoryIds = _context.Set<CategoryAssignee>()
                .Where(ca => ca.AppUserId == staffScopeUserId)
                .Select(ca => ca.CategoryId);

            query = query.Where(c => c.CategoryId.HasValue && assignedCategoryIds.Contains(c.CategoryId.Value));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(c => c.CategoryId == categoryId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        if (unreadOnly)
        {
            query = query.Where(c => c.Messages.Any(m => !m.IsRead && m.SenderId != currentUserId));
        }

        if (startDate.HasValue)
        {
            query = query.Where(c => c.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(c => c.CreatedAt <= endDate.Value);
        }

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
        // 1. Get all category IDs assigned to this staff member
        var assignedCategoryIds = await _context.CategoryAssignees
            .Where(ca => ca.AppUserId == staffUserId)
            .Select(ca => ca.CategoryId)
            .ToListAsync();

        if (!assignedCategoryIds.Any())
        {
            // Staff member has no category assignments, return empty list
            return new List<Complaint>();
        }

        // 2. Fetch the assigned category names into memory (client-side) to avoid EF Core translation errors
        var assignedCategoryNames = await _context.Categories
            .Where(cat => assignedCategoryIds.Contains(cat.Id))
            .Select(cat => cat.Name)
            .ToListAsync();

        // 3. Convert those names to the ComplaintCategory enum
        var assignedEnums = new List<ComplaintCategory>();
        foreach (var name in assignedCategoryNames)
        {
            if (Enum.TryParse<ComplaintCategory>(name, ignoreCase: true, out var parsedEnum))
            {
                assignedEnums.Add(parsedEnum);
            }
        }

        // 4. Execute the main query using the pre-calculated lists
        var complaints = await _dbSet
            .Include(c => c.Student)
            .Include(c => c.CategoryEntity)
            .Where(c =>
                // Primary filter: CategoryId matches staff's assigned categories
                (c.CategoryId.HasValue && assignedCategoryIds.Contains(c.CategoryId.Value)) ||
                // Fallback filter: Category enum matches the pre-parsed enum list
                (!c.CategoryId.HasValue && assignedEnums.Contains(c.Category))
            )
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return complaints;
    }
}