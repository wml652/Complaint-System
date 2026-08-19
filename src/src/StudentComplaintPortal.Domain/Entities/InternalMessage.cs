namespace StudentComplaintPortal.Domain.Entities;

public class InternalMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public required string SenderId { get; set; }
    public string? Content { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public string? OriginalContent { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public AppUser Sender { get; set; } = null!;

    public ICollection<InternalAttachment> Attachments { get; set; } = new List<InternalAttachment>();
}