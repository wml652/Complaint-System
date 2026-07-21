using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data.Repositories;

public interface IComplaintRepository : IGenericRepository<Complaint>
{
    Task<IEnumerable<Complaint>> GetByStudentIdAsync(string studentId);
    Task<Complaint?> GetByIdWithMessagesAsync(int id);
}
