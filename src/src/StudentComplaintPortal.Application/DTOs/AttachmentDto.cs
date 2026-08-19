using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.DTOs;

public class AttachmentDto
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}
