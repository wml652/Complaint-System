using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Data.Seeding;

public static class DbSeeder
{
    public static async Task SeedDataAsync(AppDbContext context)
    {
        if (await context.Categories.AnyAsync())
        {
            return; // Already seeded
        }

        #region Seed Categories with Attachment Rules
        var categories = new List<Category>
        {
            new Category
            {
                Name = "Admission Issues",
                Description = "Issues related to admission process, cancellation, or transfer",
                IsActive = true,
                AttachmentRules = new List<CategoryAttachmentRule>
                {
                    new CategoryAttachmentRule
                    {
                        FileType = "Photo",
                        MaxFileCount = 5,
                        MaxFileSizeBytes = 10 * 1024 * 1024,
                        IsRequired = true
                    },
                    new CategoryAttachmentRule
                    {
                        FileType = "Video",
                        MaxFileCount = 2,
                        MaxFileSizeBytes = 100 * 1024 * 1024,
                        IsRequired = false
                    }
                }
            },
            new Category
            {
                Name = "Academic Concerns",
                Description = "Grade disputes, course selection, or academic performance issues",
                IsActive = true,
                AttachmentRules = new List<CategoryAttachmentRule>
                {
                    new CategoryAttachmentRule
                    {
                        FileType = "Photo",
                        MaxFileCount = 10,
                        MaxFileSizeBytes = 5 * 1024 * 1024,
                        IsRequired = false
                    }
                }
            },
            new Category
            {
                Name = "Fee & Dues",
                Description = "Payment plans, fee refunds, or financial aid disputes",
                IsActive = true,
                AttachmentRules = new List<CategoryAttachmentRule>
                {
                    new CategoryAttachmentRule
                    {
                        FileType = "Photo",
                        MaxFileCount = 3,
                        MaxFileSizeBytes = 5 * 1024 * 1024,
                        IsRequired = true
                    }
                }
            },
            new Category
            {
                Name = "Campus Safety",
                Description = "Security concerns, facility issues, or safety complaints",
                IsActive = true,
                AttachmentRules = new List<CategoryAttachmentRule>
                {
                    new CategoryAttachmentRule
                    {
                        FileType = "Photo",
                        MaxFileCount = 5,
                        MaxFileSizeBytes = 15 * 1024 * 1024,
                        IsRequired = true
                    },
                    new CategoryAttachmentRule
                    {
                        FileType = "Video",
                        MaxFileCount = 1,
                        MaxFileSizeBytes = 200 * 1024 * 1024,
                        IsRequired = false
                    }
                }
            },
            new Category
            {
                Name = "Hostel & Accommodation",
                Description = "Hostel issues, room allocation, or accommodation complaints",
                IsActive = true,
                AttachmentRules = new List<CategoryAttachmentRule>
                {
                    new CategoryAttachmentRule
                    {
                        FileType = "Photo",
                        MaxFileCount = 5,
                        MaxFileSizeBytes = 10 * 1024 * 1024,
                        IsRequired = false
                    }
                }
            }
        };

        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();
        #endregion

        #region Get Staff Users and Assign Categories
        var staffUsers = await context.Users
            .Where(u => u.Role == UserRole.Staff)
            .ToListAsync();

        if (staffUsers.Any())
        {
            var categoryAssignees = new List<CategoryAssignee>();
            var savedCategories = await context.Categories.ToListAsync();

            for (int i = 0; i < savedCategories.Count; i++)
            {
                var staffMember = staffUsers[i % staffUsers.Count];
                categoryAssignees.Add(new CategoryAssignee
                {
                    CategoryId = savedCategories[i].Id,
                    AppUserId = staffMember.Id
                });
            }

            await context.CategoryAssignees.AddRangeAsync(categoryAssignees);
            await context.SaveChangesAsync();
        }
        #endregion

        #region Seed Complaints
        var studentUser = await context.Users
            .FirstOrDefaultAsync(u => u.Email == "student@test.com");

        if (studentUser != null)
        {
            var complaintsList = new List<Complaint>
            {
                new Complaint
                {
                    Title = "Unable to change admission status",
                    Description = "I need to change my admission from Full-time to Part-time but the portal is not allowing me to do so.",
                    Category = ComplaintCategory.Academic,
                    Status = ComplaintStatus.Open,
                    StudentId = studentUser.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new Complaint
                {
                    Title = "Grade dispute for CS101",
                    Description = "I believe my grade in Computer Science 101 is incorrect. I scored well on the assignments.",
                    Category = ComplaintCategory.Academic,
                    Status = ComplaintStatus.InProgress,
                    StudentId = studentUser.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    UpdatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new Complaint
                {
                    Title = "Missing refund for previous semester",
                    Description = "I submitted a withdrawal request last semester and paid the fees but haven't received the refund yet.",
                    Category = ComplaintCategory.Administrative,
                    Status = ComplaintStatus.Open,
                    StudentId = studentUser.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    UpdatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new Complaint
                {
                    Title = "Broken lighting in hostel corridor",
                    Description = "The corridor lights on the 3rd floor have been broken for 2 weeks and it's becoming a safety hazard.",
                    Category = ComplaintCategory.Hostel,
                    Status = ComplaintStatus.Resolved,
                    StudentId = studentUser.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    UpdatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new Complaint
                {
                    Title = "Roommate change request",
                    Description = "I would like to request a room change in the hostel due to compatibility issues with my current roommate.",
                    Category = ComplaintCategory.Hostel,
                    Status = ComplaintStatus.Open,
                    StudentId = studentUser.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    UpdatedAt = DateTime.UtcNow.AddDays(-2)
                }
            };

            await context.Complaints.AddRangeAsync(complaintsList);
            await context.SaveChangesAsync();

            #region Seed Messages
            var adminUser = await context.Users
                .FirstOrDefaultAsync(u => u.Email == "admin@test.com");

            if (adminUser != null)
            {
                var savedComplaints = await context.Complaints
                    .Where(c => c.StudentId == studentUser.Id)
                    .ToListAsync();

                var messages = new List<Message>();

                // Add messages to first complaint
                if (savedComplaints.Count > 0)
                {
                    messages.Add(new Message
                    {
                        ComplaintId = savedComplaints[0].Id,
                        SenderId = studentUser.Id,
                        Content = "Hi, I'm having issues with changing my admission status. Can you help?",
                        SentAt = DateTime.UtcNow.AddDays(-5),
                        IsRead = true,
                        ReadAt = DateTime.UtcNow.AddDays(-5).AddHours(1)
                    });

                    messages.Add(new Message
                    {
                        ComplaintId = savedComplaints[0].Id,
                        SenderId = adminUser.Id,
                        Content = "Hello! I'll look into this for you. Can you provide more details about which aspect you're trying to change?",
                        SentAt = DateTime.UtcNow.AddDays(-5).AddHours(2),
                        IsRead = true,
                        ReadAt = DateTime.UtcNow.AddDays(-5).AddHours(3)
                    });

                    messages.Add(new Message
                    {
                        ComplaintId = savedComplaints[0].Id,
                        SenderId = studentUser.Id,
                        Content = "I want to change from Full-time to Part-time enrollment.",
                        SentAt = DateTime.UtcNow.AddDays(-4).AddHours(1),
                        IsRead = true,
                        ReadAt = DateTime.UtcNow.AddDays(-4).AddHours(2)
                    });

                    messages.Add(new Message
                    {
                        ComplaintId = savedComplaints[0].Id,
                        SenderId = adminUser.Id,
                        Content = "I see. This requires special permission from the Registrar. I'm processing your request now.",
                        SentAt = DateTime.UtcNow.AddDays(-4).AddHours(3),
                        IsRead = true,
                        ReadAt = DateTime.UtcNow.AddDays(-4).AddHours(4)
                    });
                }

                // Add messages to second complaint
                if (savedComplaints.Count > 1)
                {
                    messages.Add(new Message
                    {
                        ComplaintId = savedComplaints[1].Id,
                        SenderId = studentUser.Id,
                        Content = "My grade in CS101 seems wrong. I did well on assignments.",
                        SentAt = DateTime.UtcNow.AddDays(-3),
                        IsRead = true,
                        ReadAt = DateTime.UtcNow.AddDays(-3).AddHours(1)
                    });

                    messages.Add(new Message
                    {
                        ComplaintId = savedComplaints[1].Id,
                        SenderId = adminUser.Id,
                        Content = "I'm forwarding this to the professor for review. They will contact you shortly.",
                        SentAt = DateTime.UtcNow.AddDays(-3).AddHours(1),
                        IsRead = true,
                        ReadAt = DateTime.UtcNow.AddDays(-3).AddHours(2)
                    });
                }

                // Add messages to third complaint
                if (savedComplaints.Count > 2)
                {
                    messages.Add(new Message
                    {
                        ComplaintId = savedComplaints[2].Id,
                        SenderId = studentUser.Id,
                        Content = "I withdrew last semester and paid the fees. Where is my refund?",
                        SentAt = DateTime.UtcNow.AddDays(-7),
                        IsRead = true,
                        ReadAt = DateTime.UtcNow.AddDays(-7).AddHours(1)
                    });

                    messages.Add(new Message
                    {
                        ComplaintId = savedComplaints[2].Id,
                        SenderId = adminUser.Id,
                        Content = "Let me check your refund status. Can you provide your student ID and transaction reference?",
                        SentAt = DateTime.UtcNow.AddDays(-7).AddHours(2),
                        IsRead = true,
                        ReadAt = DateTime.UtcNow.AddDays(-7).AddHours(3)
                    });
                }

                // Add messages to fourth complaint (resolved)
                if (savedComplaints.Count > 3)
                {
                    messages.Add(new Message
                    {
                        ComplaintId = savedComplaints[3].Id,
                        SenderId = studentUser.Id,
                        Content = "The lights in the 3rd floor corridor are broken. This is dangerous!",
                        SentAt = DateTime.UtcNow.AddDays(-10),
                        IsRead = true,
                        ReadAt = DateTime.UtcNow.AddDays(-10).AddHours(1)
                    });

                    messages.Add(new Message
                    {
                        ComplaintId = savedComplaints[3].Id,
                        SenderId = adminUser.Id,
                        Content = "Thank you for reporting this. I've contacted maintenance immediately.",
                        SentAt = DateTime.UtcNow.AddDays(-10).AddHours(1),
                        IsRead = true,
                        ReadAt = DateTime.UtcNow.AddDays(-10).AddHours(2)
                    });

                    messages.Add(new Message
                    {
                        ComplaintId = savedComplaints[3].Id,
                        SenderId = adminUser.Id,
                        Content = "Good news! The lights have been repaired. Thank you for your patience.",
                        SentAt = DateTime.UtcNow.AddDays(-1),
                        IsRead = true,
                        ReadAt = DateTime.UtcNow.AddDays(-1).AddHours(1)
                    });
                }

                if (messages.Any())
                {
                    await context.Messages.AddRangeAsync(messages);
                    await context.SaveChangesAsync();
                }
            }
            #endregion

            #region Seed Message Quotas
            var quotaRecords = new List<MessageQuota>();
            var allComplaints = await context.Complaints
                .Where(c => c.StudentId == studentUser.Id)
                .ToListAsync();

            foreach (var complaint in allComplaints)
            {
                quotaRecords.Add(new MessageQuota
                {
                    ComplaintId = complaint.Id,
                    StudentId = studentUser.Id,
                    MessagesRemaining = MessageQuota.MAX_MESSAGES_PER_STAFF_RESPONSE,
                    LastStaffMessageAt = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            if (quotaRecords.Any())
            {
                await context.MessageQuotas.AddRangeAsync(quotaRecords);
                await context.SaveChangesAsync();
            }
            #endregion
        }
        #endregion
    }
}

