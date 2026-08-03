using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Exceptions;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.Services;

public class MessageService : IMessageService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public MessageService(IUnitOfWork unitOfWork, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<MessageDto> SendMessageAsync(int complaintId, string senderId, string? content, List<int>? attachmentIds = null)
    {
        // Verify complaint exists
        var complaint = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        if (complaint == null)
        {
            throw new NotFoundException($"Complaint with ID {complaintId} not found.");
        }

        if (complaint.Status == ComplaintStatus.Closed)
        {
            throw new ComplaintClosedException("This complaint is closed. New messages can't be sent.");
        }

        var message = new Message
        {
            ComplaintId = complaintId,
            SenderId = senderId,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        await _unitOfWork.Messages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();

        // Link attachments if provided
        if (attachmentIds != null && attachmentIds.Any())
        {
            var attachments = await _unitOfWork.Attachments.FindAsync(a => attachmentIds.Contains(a.Id));
            foreach (var attachment in attachments)
            {
                attachment.MessageId = message.Id;
                _unitOfWork.Attachments.Update(attachment);
            }
            await _unitOfWork.SaveChangesAsync();
        }

        // Reload with sender and attachments info
        var messages = await _unitOfWork.Messages.GetByComplaintIdAsync(complaintId);
        var created = messages.FirstOrDefault(m => m.Id == message.Id);
        
        // Notify the other party
        var senderUser = await GetUserByIdAsync(senderId);
        if (senderUser != null)
        {
            if (senderUser.Role == UserRole.Student)
            {
                // Notify all admins
                var admins = await GetAllAdminsAsync();
                foreach (var admin in admins)
                {
                    await _notificationService.NotifyAsync(
                        admin.Id,
                        $"New message from {senderUser.FullName} on complaint #{complaintId}",
                        NotificationType.NewMessage
                    );
                }
            }
            else if (senderUser.Role == UserRole.Admin)
            {
                // Notify the complaint's student
                await _notificationService.NotifyAsync(
                    complaint.StudentId,
                    $"New message from admin on your complaint: {complaint.Title}",
                    NotificationType.NewMessage
                );
            }
        }

        return MapToDto(created!);
    }

    public async Task<IEnumerable<MessageDto>> GetConversationAsync(int complaintId)
    {
        var messages = await _unitOfWork.Messages.GetByComplaintIdAsync(complaintId);
        return messages.Select(MapToDto);
    }

    public async Task<MessageDto> GetMessageByIdAsync(int messageId)
    {
        var messages = await _unitOfWork.Messages.FindAsync(m => m.Id == messageId);
        var message = messages.FirstOrDefault();
        
        if (message == null)
        {
            throw new NotFoundException($"Message with ID {messageId} not found.");
        }

        return MapToDto(message);
    }

    private async Task<AppUser?> GetUserByIdAsync(string userId)
    {
        // This is a workaround since we don't have a user repository
        // We'll fetch from a message or complaint that belongs to this user
        var messages = await _unitOfWork.Messages.FindAsync(m => m.SenderId == userId);
        return messages.FirstOrDefault()?.Sender;
    }

    private async Task<List<AppUser>> GetAllAdminsAsync()
    {
        // This is a workaround to get all admin users
        // In a real implementation, we'd have a user repository
        var complaints = await _unitOfWork.Complaints.GetAllAsync();
        var allUsers = complaints.Select(c => c.Student).Distinct().ToList();
        
        // For now, we'll return an empty list and rely on the controller to inject UserManager
        // This will be handled at the SignalR/Controller level
        return new List<AppUser>();
    }

    private MessageDto MapToDto(Message message)
    {
        return new MessageDto
        {
            Id = message.Id,
            ComplaintId = message.ComplaintId,
            SenderId = message.SenderId,
            SenderName = message.Sender?.FullName ?? string.Empty,
            Content = message.Content,
            SentAt = message.SentAt,
            IsRead = message.IsRead,
            ReadAt = message.ReadAt,
            Attachments = message.Attachments?.Select(a => new AttachmentDto
            {
                Id = a.Id,
                MessageId = a.MessageId,
                FileUrl = a.FileUrl,
                FileType = a.FileType.ToString(),
                FileSizeBytes = a.FileSizeBytes,
                UploadedAt = a.UploadedAt
            }).ToList() ?? new List<AttachmentDto>()
        };
    }


    public async Task MarkAllAsReadAsync(int complaintId, string readerUserId)
    {
        var dbMessages = await _unitOfWork.Messages.FindAsync(
            m => m.ComplaintId == complaintId && m.SenderId != readerUserId && m.ReadAt == null);

        var now = DateTime.UtcNow;
        bool anyDbUpdated = false;

        foreach (var message in dbMessages)
        {
            message.ReadAt = now;
            message.IsRead = true;
            _unitOfWork.Messages.Update(message);
            anyDbUpdated = true;
        }

        if (anyDbUpdated)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _bufferService.MarkAsRead(complaintId, readerUserId);
    }
}