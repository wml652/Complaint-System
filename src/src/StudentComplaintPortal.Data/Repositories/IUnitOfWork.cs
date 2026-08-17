using StudentComplaintPortal.Data;

namespace StudentComplaintPortal.Data.Repositories;

public interface IUnitOfWork : IDisposable
{
    IComplaintRepository Complaints { get; }
    IMessageRepository Messages { get; }
    IAttachmentRepository Attachments { get; }
    INotificationRepository Notifications { get; }
    ICategoryRepository Categories { get; }
    IConversationRepository Conversations { get; }
    IMessageQuotaRepository MessageQuotas { get; }
    IPermissionRepository Permissions { get; }

    Task<int> SaveChangesAsync();
}