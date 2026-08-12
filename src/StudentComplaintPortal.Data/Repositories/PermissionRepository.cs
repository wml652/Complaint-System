using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data.Repositories;

public class PermissionRepository : GenericRepository<Permission>, IPermissionRepository
{
    public PermissionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<Permission>> GetAllOrderedAsync()
    {
        return await _dbSet
            .OrderBy(p => p.Module).ThenBy(p => p.DisplayName)
            .ToListAsync();
    }
}