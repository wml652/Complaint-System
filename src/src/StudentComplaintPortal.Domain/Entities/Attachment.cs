using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Domain.Entities;

public class Attachment
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public required string FileUrl { get; set; }
    public FileType FileType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }

    // Navigation property
    public Message Message { get; set; } = null!;
}
