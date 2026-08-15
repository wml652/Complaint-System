using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Complaint> Complaints { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Attachment> Attachments { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<ConversationParticipant> ConversationParticipants { get; set; }
    public DbSet<InternalMessage> InternalMessages { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<CategoryAttachmentRule> CategoryAttachmentRules { get; set; }
    public DbSet<CategoryAssignee> CategoryAssignees { get; set; }
    public DbSet<MessageQuota> MessageQuotas { get; set; }

    public DbSet<InternalAttachment> InternalAttachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Complaint configuration
        modelBuilder.Entity<Complaint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.Category).HasConversion<string>();

            entity.HasOne(e => e.Student)
                .WithMany(u => u.Complaints)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // NEW: Add relationship to Category table
            entity.HasOne(e => e.CategoryEntity)
                .WithMany(c => c.Complaints)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);  // Don when category is deleted

            entity.HasIndex(e => e.StudentId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.CategoryId);  // Add index for performance
        });

        // Message configuration
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).HasMaxLength(5000);

            entity.HasOne(e => e.Complaint)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ComplaintId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ReadBy)
                .WithMany()
                .HasForeignKey(e => e.ReadByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ComplaintId);
            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => e.SentAt);
        });

        // Attachment configuration
        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FileType).HasConversion<string>();

            entity.HasOne(e => e.Message)
                .WithMany(m => m.Attachments)
                .HasForeignKey(e => e.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.MessageId);
        });

        // Notification configuration
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(500);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsRead });
            entity.HasIndex(e => e.CreatedAt);
        });

        // AppUser configuration
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Role).HasConversion<string>();
        });

        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired();

            entity.HasMany(c => c.AttachmentRules)
                .WithOne(r => r.Category)
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(c => c.Assignees)
                .WithOne(a => a.Category)
                .HasForeignKey(a => a.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.IsActive);
        });

        // CategoryAttachmentRule configuration
        modelBuilder.Entity<CategoryAttachmentRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileType).HasConversion<string>();
            entity.Property(e => e.MaxFileCount).IsRequired();
            entity.Property(e => e.MaxFileSizeBytes).IsRequired();
            entity.Property(e => e.IsRequired).IsRequired();

            entity.HasIndex(e => e.CategoryId);
        });

        // CategoryAssignee configuration
        modelBuilder.Entity<CategoryAssignee>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Assignees)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AppUser)
                .WithMany()
                .HasForeignKey(e => e.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.AppUserId);
        });
        // Conversation configuration
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);

            entity.HasMany(c => c.Participants)
                .WithOne(p => p.Conversation)
                .HasForeignKey(p => p.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(c => c.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ConversationParticipant configuration
        modelBuilder.Entity<ConversationParticipant>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ConversationId, e.UserId }).IsUnique();
        });

        // InternalMessage configuration
        modelBuilder.Entity<InternalMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).HasMaxLength(5000);

            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.SentAt);
        });
        // Attachments for internalMessages
        modelBuilder.Entity<InternalAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileUrl).IsRequired().HasMaxLength(500);

            entity.HasOne(e => e.InternalMessage)
                .WithMany(m => m.Attachments)
                .HasForeignKey(e => e.InternalMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.InternalMessageId);
        });

        // MessageQuota configuration
        modelBuilder.Entity<MessageQuota>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.ComplaintId, e.StudentId }).IsUnique();

            entity.HasOne(e => e.Complaint)
                .WithMany()
                .HasForeignKey(e => e.ComplaintId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.MessagesRemaining)
                .HasDefaultValue(MessageQuota.MAX_MESSAGES_PER_STAFF_RESPONSE);
        });
    }
}