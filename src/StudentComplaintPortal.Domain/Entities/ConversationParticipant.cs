namespace StudentComplaintPortal.Domain.Entities;

public class ConversationParticipant
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public required string UserId { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
