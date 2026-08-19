using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Exceptions;
using StudentComplaintPortal.Application.Services.FileStorage;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.Services;

public class AttachmentService : IAttachmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IMessageService _messageService;

    public AttachmentService(IUnitOfWork unitOfWork, IFileStorageService fileStorageService, IMessageService messageService)
    {
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _messageService = messageService;
    }

    public async Task<AttachmentDto> CreateAttachmentAsync(int messageId, string fileUrl, FileType fileType, long fileSizeBytes)
    {
        var attachment = new Attachment
        {
            MessageId = messageId,
            FileUrl = fileUrl,
            FileType = fileType,
            FileSizeBytes = fileSizeBytes,
            UploadedAt = DateTime.UtcNow
        };

        await _unitOfWork.Attachments.AddAsync(attachment);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(attachment);
    }

    public async Task<MessageDto> CreateMessageWithAttachmentAsync(int complaintId, string senderId, Stream fileStream, string fileName, string contentType, FileType fileType, string? content = null)
    {
        // Verify complaint exists
        var complaint = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        if (complaint == null)
        {
            throw new NotFoundException($"Complaint with ID {complaintId} not found.");
        }

        //Verify Complaint status if closed
        if (complaint.Status == ComplaintStatus.Closed)
        {
            throw new ComplaintClosedException("This complaint is closed. New messages can't be sent.");
        }

        // Upload file
        var fileUrl = await _fileStorageService.UploadAsync(fileStream, fileName, contentType, fileType, complaintId);

        // Create message with attachment
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

        // Create attachment linked to message
        var attachment = new Attachment
        {
            MessageId = message.Id,
            FileUrl = fileUrl,
            FileType = fileType,
            FileSizeBytes = fileStream.Length,
            UploadedAt = DateTime.UtcNow
        };

        await _unitOfWork.Attachments.AddAsync(attachment);
        await _unitOfWork.SaveChangesAsync();

        // Reload message with attachments
        return await _messageService.GetMessageByIdAsync(message.Id);
    }

    private AttachmentDto MapToDto(Attachment attachment)
    {
        return new AttachmentDto
        {
            Id = attachment.Id,
            MessageId = attachment.MessageId,
            FileUrl = attachment.FileUrl,
            FileType = attachment.FileType.ToString(),
            FileSizeBytes = attachment.FileSizeBytes,
            UploadedAt = attachment.UploadedAt
        };
    }
}
