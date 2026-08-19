using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data.Repositories;

public interface ICategoryRepository : IGenericRepository<Category>
{
    Task<IEnumerable<Category>> GetAllActiveWithDetailsAsync();
    Task<Category?> GetByIdWithDetailsAsync(int id);
    Task<IEnumerable<Category>> GetAssignedToStaffAsync(string staffUserId);
}