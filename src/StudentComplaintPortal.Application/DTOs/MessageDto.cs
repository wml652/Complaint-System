namespace StudentComplaintPortal.Application.DTOs;

public class MessageDto
{
    public int Id { get; set; }
    public int ComplaintId { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string? Content { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public List<AttachmentDto> Attachments { get; set; } = new List<AttachmentDto>();
}
