namespace StudentComplaintPortal.Application.DTOs;

public class ConversationDto
{
    public int Id { get; set; }
    public required string Type { get; set; }   // "Direct" or "Group"
    public string? Name { get; set; }           // Group name, or other persons name for direct
    public string? OtherUserId { get; set; }    // only for direct
    public int UnreadCount { get; set; }
    public string? LastMessagePreview { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

public class InternalMessageDto
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public required string SenderId { get; set; }
    public required string SenderName { get; set; }
    public string? Content { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public string? OriginalContent { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<InternalAttachmentDto> Attachments { get; set; } = new();
}
public class InternalAttachmentDto   
{
    public int Id { get; set; }
    public required string FileUrl { get; set; }
    public required string FileType { get; set; }
    public long FileSizeBytes { get; set; }
}

public class ParticipantDto
{
    public required string UserId { get; set; }
    public required string FullName { get; set; }
    public DateTime? LastReadAt { get; set; }
}