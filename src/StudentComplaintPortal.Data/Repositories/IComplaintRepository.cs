using StudentComplaintPortal.Domain.Entities;
namespace StudentComplaintPortal.Data.Repositories;
public interface IComplaintRepository : IGenericRepository<Complaint>
{
    Task<IEnumerable<Complaint>> GetByStudentIdAsync(string studentId);
    Task<Complaint?> GetByIdWithMessagesAsync(int id);

    // Complaints whose category has the given staff member as an assignee
    Task<IEnumerable<Complaint>> GetAssignedToStaffAsync(string staffUserId);
}