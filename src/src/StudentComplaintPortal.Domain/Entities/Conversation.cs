namespace StudentComplaintPortal.Domain.Entities;

public enum ConversationType { Direct, Group, Query}

public class Conversation
{
    public int Id { get; set; }
    public ConversationType Type { get; set; }
    public string? Name { get; set; }   // null for Direct, "Team" for the pinned group
    public DateTime CreatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }

    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public ICollection<InternalMessage> Messages { get; set; } = new List<InternalMessage>();
}
