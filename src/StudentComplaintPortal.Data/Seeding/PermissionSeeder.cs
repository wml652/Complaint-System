using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data.Seeding;

public static class PermissionSeeder
{
    // Central catalog — adding a new permission here (or later via DB)
    // is the ONLY thing needed to make it show up in the Assign Roles UI.
    public static readonly (string Code, string DisplayName, string Module)[] DefaultPermissions =
    {
        ("Users.Add",                     "Add new users",              "Users"),
        ("Users.Edit",                    "Edit user details",          "Users"),
        ("Users.Delete",                  "Delete users",                "Users"),
        ("Complaints.ViewAll",            "View all complaints",        "Complaints"),
        ("Complaints.ViewAssignedOnly",   "View only assigned complaints", "Complaints"),
        ("Complaints.ChangeStatus",       "Change complaint status",    "Complaints"),
        ("Complaints.Delete",             "Delete complaints",           "Complaints"),
        ("Complaints.AssignToStaff",      "Assign complaints to staff",  "Complaints"),
        ("Roles.Manage",                  "Create/edit roles and permissions", "Roles"),
    };//Users.add/delete/edit Complaints.Delete/AssignToStaff  Roles.Manage not yet implemented ...coming soon

    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.Permissions.AnyAsync())
        {
            return; // already seeded
        }

        foreach (var (code, displayName, module) in DefaultPermissions)
        {
            context.Permissions.Add(new Permission
            {
                Code = code,
                DisplayName = displayName,
                Module = module
            });
        }

        await context.SaveChangesAsync();
    }
}
