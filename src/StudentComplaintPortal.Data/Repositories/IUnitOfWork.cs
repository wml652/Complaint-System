using StudentComplaintPortal.Data;

namespace StudentComplaintPortal.Data.Repositories;

public interface IUnitOfWork : IDisposable
{
    IComplaintRepository Complaints { get; }
    IMessageRepository Messages { get; }
    IAttachmentRepository Attachments { get; }
    INotificationRepository Notifications { get; }

    AppDbContext Context { get; }
    Task<int> SaveChangesAsync();
}
