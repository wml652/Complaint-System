namespace StudentComplaintPortal.Domain.Entities;

public class ConversationParticipant
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public required string UserId { get; set; }

    // Ye member ne conversation ko aakhri baar kab tak parh liya tha (group "seen by all" ke liye zaroori)
    public DateTime? LastReadAt { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
