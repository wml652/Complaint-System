namespace StudentComplaintPortal.Domain.Entities;

public class Message
{
    public int Id { get; set; }
    public int ComplaintId { get; set; }
    public required string SenderId { get; set; }
    public string? Content { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }   // to show the tick when message is read

    public bool IsRead { get; set; }

    // Navigation properties
    public Complaint Complaint { get; set; } = null!;
    public AppUser Sender { get; set; } = null!;
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
