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
    private readonly MessageBufferService _bufferService;

    public MessageService(IUnitOfWork unitOfWork, INotificationService notificationService, MessageBufferService bufferService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _bufferService = bufferService;
    }

    public async Task<MessageDto> SendMessageAsync(int complaintId, string senderId, string? content, List<int>? attachmentIds = null)
    {
        var complaint = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        if (complaint == null)
        {
            throw new NotFoundException($"Complaint with ID {complaintId} not found.");
        }

        var message = new Message
        {
            ComplaintId = complaintId,
            SenderId = senderId,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        // Fetch user early so we can attach it to the DTO for SignalR broadcasting
        var senderUser = await GetUserByIdAsync(senderId);
        message.Sender = senderUser;

        // If there are attachments, we MUST save immediately to generate the Message.Id for the foreign key
        if (attachmentIds != null && attachmentIds.Any())
        {
            await _unitOfWork.Messages.AddAsync(message);
            await _unitOfWork.SaveChangesAsync();

            var attachments = await _unitOfWork.Attachments.FindAsync(a => attachmentIds.Contains(a.Id));
            foreach (var attachment in attachments)
            {
                attachment.MessageId = message.Id;
                _unitOfWork.Attachments.Update(attachment);
            }
            await _unitOfWork.SaveChangesAsync();

            // Reload attachments for the DTO
            var reloadedMessages = await _unitOfWork.Messages.GetByComplaintIdAsync(complaintId);
            message = reloadedMessages.FirstOrDefault(m => m.Id == message.Id) ?? message;
        }
        else
        {
            // No attachments: Add to memory buffer instead of hitting the database
            _bufferService.AddMessage(complaintId, message);
        }

        // Notify the other party
        try
        {
            // Notify the other party
            if (senderUser != null)
            {
                if (senderUser.Role == UserRole.Student)
                {
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
                    await _notificationService.NotifyAsync(
                        complaint.StudentId,
                        $"New message from admin on your complaint: {complaint.Title}",
                        NotificationType.NewMessage
                    );
                }
            }
        }
        catch (Exception ex)
        {
            // This stops the HttpRequestException from crashing your chat!
            Console.WriteLine($"[NOTIFICATION ERROR] Failed to send notification: {ex.Message}");
        }
        // ==========================================

        return MapToDto(message); // Ensure it successfully reaches this line!
    }

    public async Task<IEnumerable<MessageDto>> GetConversationAsync(int complaintId)
    {
        // 1. Fetch saved messages from the Database
        var dbMessages = await _unitOfWork.Messages.GetByComplaintIdAsync(complaintId);
        var messageDtos = dbMessages.Select(MapToDto).ToList();

        // 2. Fetch unsaved messages from the Memory Buffer
        var bufferedMessages = _bufferService.GetBufferedMessages(complaintId);
        var bufferedDtos = bufferedMessages.Select(MapToDto).ToList();

        // 3. Combine both lists and order them by time so the chat flows perfectly
        return messageDtos.Concat(bufferedDtos).OrderBy(m => m.SentAt);
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
        var messages = await _unitOfWork.Messages.FindAsync(m => m.SenderId == userId);
        return messages.FirstOrDefault()?.Sender;
    }

    private async Task<List<AppUser>> GetAllAdminsAsync()
    {
        var complaints = await _unitOfWork.Complaints.GetAllAsync();
        var allUsers = complaints.Select(c => c.Student).Distinct().ToList();
        return new List<AppUser>();
    }

    private MessageDto MapToDto(Message message)
    {
        return new MessageDto
        {
            Id = message.Id, // Will be 0 if currently buffered, which is fine for UI chat display
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