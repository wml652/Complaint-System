using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.Services;

public interface IAttachmentService
{
    Task<AttachmentDto> CreateAttachmentAsync(int messageId, string fileUrl, FileType fileType, long fileSizeBytes);
    Task<MessageDto> CreateMessageWithAttachmentAsync(int complaintId, string senderId, Stream fileStream, string fileName, string contentType, FileType fileType, string? content = null);
}
