using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Domain.Entities;

public class InternalAttachment
{
    public int Id { get; set; }
    public int InternalMessageId { get; set; }
    public required string FileUrl { get; set; }
    public FileType FileType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }

    public InternalMessage InternalMessage { get; set; } = null!;
}