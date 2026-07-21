namespace StudentComplaintPortal.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IComplaintRepository? _complaints;
    private IMessageRepository? _messages;
    private IAttachmentRepository? _attachments;
    private INotificationRepository? _notifications;

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

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
