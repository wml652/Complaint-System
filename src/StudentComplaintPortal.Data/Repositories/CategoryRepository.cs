using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data.Repositories;

public class CategoryRepository : GenericRepository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Category>> GetAllActiveWithDetailsAsync()
    {
        return await _dbSet
            .Where(c => c.IsActive)
            .Include(c => c.AttachmentRules)
            .Include(c => c.Assignees)
            .ToListAsync();
    }

    public async Task<Category?> GetByIdWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(c => c.AttachmentRules)
            .Include(c => c.Assignees)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Category>> GetAssignedToStaffAsync(string staffUserId)
    {
        return await _dbSet
            .Where(c => c.Assignees.Any(a => a.AppUserId == staffUserId))
            .Include(c => c.AttachmentRules)
            .Include(c => c.Assignees)
            .ToListAsync();
    }
}