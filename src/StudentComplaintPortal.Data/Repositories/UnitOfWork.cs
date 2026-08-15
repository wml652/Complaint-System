using StudentComplaintPortal.Data;

namespace StudentComplaintPortal.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IComplaintRepository? _complaints;
    private IMessageRepository? _messages;
    private IAttachmentRepository? _attachments;
    private INotificationRepository? _notifications;
    private ICategoryRepository? _categories;
    private IConversationRepository? _conversations;
    private IMessageQuotaRepository? _messageQuotas;
    private IPermissionRepository? _permissions;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IComplaintRepository Complaints =>
        _complaints ??= new ComplaintRepository(_context);

    public IMessageRepository Messages =>
        _messages ??= new MessageRepository(_context);

    public IAttachmentRepository Attachments =>
        _attachments ??= new AttachmentRepository(_context);

    public INotificationRepository Notifications =>
        _notifications ??= new NotificationRepository(_context);

    public ICategoryRepository Categories =>
        _categories ??= new CategoryRepository(_context);

    public IConversationRepository Conversations =>
        _conversations ??= new ConversationRepository(_context);

    public IMessageQuotaRepository MessageQuotas =>
        _messageQuotas ??= new MessageQuotaRepository(_context);

    public IPermissionRepository Permissions =>
        _permissions ??= new PermissionRepository(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
