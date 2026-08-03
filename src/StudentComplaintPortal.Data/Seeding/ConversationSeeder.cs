using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Data;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Data.Seeding;

public static class ConversationSeeder
{
    private const string TeamGroupName = "Team";

    public static async Task SeedAsync(AppDbContext context)
    {
        // Step 1: "Team" group ka wajood confirm karo, warna banao
        var teamGroup = await context.Conversations
            .FirstOrDefaultAsync(c => c.Type == ConversationType.Group && c.Name == TeamGroupName);

        if (teamGroup == null)
        {
            teamGroup = new Conversation
            {
                Type = ConversationType.Group,
                Name = TeamGroupName,
                CreatedAt = DateTime.UtcNow
            };
            context.Conversations.Add(teamGroup);
            await context.SaveChangesAsync();
        }

        // Step 2: har Staff/Admin ko is group mein add karo agar already nahi hai
        var staffAndAdminUsers = await context.Users
            .Where(u => u.Role == UserRole.Staff || u.Role == UserRole.Admin)
            .ToListAsync();

        var existingParticipantIds = await context.ConversationParticipants
            .Where(p => p.ConversationId == teamGroup.Id)
            .Select(p => p.UserId)
            .ToListAsync();

        foreach (var user in staffAndAdminUsers)
        {
            if (!existingParticipantIds.Contains(user.Id))
            {
                context.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = teamGroup.Id,
                    UserId = user.Id
                });
            }
        }

        await context.SaveChangesAsync();
    }
}