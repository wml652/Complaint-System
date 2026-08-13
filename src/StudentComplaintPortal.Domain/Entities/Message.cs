namespace StudentComplaintPortal.Domain.Entities;

public class Message
{
    public int Id { get; set; }
    public int ComplaintId { get; set; }
    public required string SenderId { get; set; }
    public string? Content { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ReadByUserId { get; set; }
    public bool IsRead { get; set; }
    public bool IsVoiceMessage { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public string? OriginalContent { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public Complaint Complaint { get; set; } = null!;
    public AppUser Sender { get; set; } = null!;
    public AppUser? ReadBy { get; set; }
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}

