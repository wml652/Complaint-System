namespace StudentComplaintPortal.Application.DTOs;

public class PermissionDto
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string DisplayName { get; set; }
    public required string Module { get; set; }
    public bool IsGranted { get; set; } // checkbox state when editing a role
}

public class RoleDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public List<string> PermissionCodes { get; set; } = new();
}

public class CreateOrUpdateRoleDto
{
    public string? Id { get; set; } // null when creating
    public required string Name { get; set; }
    public List<string> SelectedPermissionCodes { get; set; } = new();
}

public class StaffUserDto
{
    public required string Id { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public List<string> AssignedRoleNames { get; set; } = new();
}

public class AssignRoleToStaffDto
{
    public required string StaffUserId { get; set; }
    public required string RoleId { get; set; }
}