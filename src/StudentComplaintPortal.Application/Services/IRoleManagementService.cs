using StudentComplaintPortal.Application.DTOs;

namespace StudentComplaintPortal.Application.Services;

public interface IRoleManagementService
{
    Task<List<PermissionDto>> GetAllPermissionsAsync();
    Task<List<RoleDto>> GetAllRolesAsync();
    Task<RoleDto?> GetRoleByIdAsync(string roleId);
    Task<(bool Success, string? Error)> CreateOrUpdateRoleAsync(CreateOrUpdateRoleDto dto);
    Task<bool> DeleteRoleAsync(string roleId);

    Task<List<StaffUserDto>> GetAllStaffUsersAsync();
    Task<(bool Success, string? Error)> AssignRoleToStaffAsync(AssignRoleToStaffDto dto);
    Task<(bool Success, string? Error)> RemoveRoleFromStaffAsync(AssignRoleToStaffDto dto);
}