using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data.Repositories;

public interface IPermissionRepository : IGenericRepository<Permission>
{
    Task<List<Permission>> GetAllOrderedAsync();
}