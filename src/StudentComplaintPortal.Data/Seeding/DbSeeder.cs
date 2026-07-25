using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Data.Seeding;

public static class DbSeeder
{
    public static async Task SeedDataAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<AppDbContext>();
        var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();

        // Check if categories already exist
        if (await context.Categories.AnyAsync())
        {
            return; // Data already seeded
        }

        // Query for specific staff members by FullName
        var mahnoorFatima = await userManager.Users.FirstOrDefaultAsync(u => u.FullName == "Mahnoor Fatima");
        var muskan = await userManager.Users.FirstOrDefaultAsync(u => u.FullName == "Muskan");
        var faizan = await userManager.Users.FirstOrDefaultAsync(u => u.FullName == "Faizan");
        var ahmed = await userManager.Users.FirstOrDefaultAsync(u => u.FullName == "Ahmed");
        var faraz = await userManager.Users.FirstOrDefaultAsync(u => u.FullName == "Faraz");
        var bisma = await userManager.Users.FirstOrDefaultAsync(u => u.FullName == "Bisma");

        // Category 1: Hostel & Mess Management
        var hostelCategory = new Category
        {
            Name = "Hostel & Mess Management",
            Description = "Issues related to hostel allocation, maintenance, and mess billing.",
            IsActive = true
        };

        // Add attachment rule for Hostel category
        hostelCategory.AttachmentRules.Add(new CategoryAttachmentRule
        {
            FileType = FileType.Photo,
            MaxFileCount = 1,
            MaxFileSizeBytes = 5 * 1024 * 1024, // 5MB
            IsRequired = true
        });

        // Assign staff to Hostel category
        if (faizan != null)
        {
            hostelCategory.Assignees.Add(new CategoryAssignee
            {
                AppUserId = faizan.Id
            });
        }
        if (ahmed != null)
        {
            hostelCategory.Assignees.Add(new CategoryAssignee
            {
                AppUserId = ahmed.Id
            });
        }

        // Category 2: Academic & Course Queries
        var academicCategory = new Category
        {
            Name = "Academic & Course Queries",
            Description = "Issues regarding curriculum, transcripts, and course registration.",
            IsActive = true
        };

        // Add attachment rule for Academic category
        academicCategory.AttachmentRules.Add(new CategoryAttachmentRule
        {
            FileType = FileType.Photo,
            MaxFileCount = 2,
            MaxFileSizeBytes = 5 * 1024 * 1024, // 5MB
            IsRequired = false
        });

        // Assign staff to Academic category
        if (muskan != null)
        {
            academicCategory.Assignees.Add(new CategoryAssignee
            {
                AppUserId = muskan.Id
            });
        }
        if (bisma != null)
        {
            academicCategory.Assignees.Add(new CategoryAssignee
            {
                AppUserId = bisma.Id
            });
        }

        // Category 3: Technical Support
        var technicalCategory = new Category
        {
            Name = "Technical Support",
            Description = "System bugs, portal login issues, or POS integration errors.",
            IsActive = true
        };

        // Add attachment rules for Technical category
        technicalCategory.AttachmentRules.Add(new CategoryAttachmentRule
        {
            FileType = FileType.Video,
            MaxFileCount = 1,
            MaxFileSizeBytes = 20 * 1024 * 1024, // 20MB
            IsRequired = false
        });

        technicalCategory.AttachmentRules.Add(new CategoryAttachmentRule
        {
            FileType = FileType.Photo,
            MaxFileCount = 1,
            MaxFileSizeBytes = 5 * 1024 * 1024, // 5MB
            IsRequired = false
        });

        // Assign staff to Technical category
        if (mahnoorFatima != null)
        {
            technicalCategory.Assignees.Add(new CategoryAssignee
            {
                AppUserId = mahnoorFatima.Id
            });
        }
        if (faraz != null)
        {
            technicalCategory.Assignees.Add(new CategoryAssignee
            {
                AppUserId = faraz.Id
            });
        }

        // Add categories to context
        context.Categories.Add(hostelCategory);
        context.Categories.Add(academicCategory);
        context.Categories.Add(technicalCategory);

        // Save changes
        await context.SaveChangesAsync();
    }
}
