using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;
using System.Security.Claims;

namespace StudentComplaintPortal.Application.Services;

public class RoleManagementService : IRoleManagementService
{
    private const string PermissionClaimType = "Permission";

    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;   // AppDbContext ki jagah

    public RoleManagementService(
        RoleManager<IdentityRole> roleManager,
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<PermissionDto>> GetAllPermissionsAsync()
    {
        var permissions = await _unitOfWork.Permissions.GetAllOrderedAsync();
        return permissions.Select(p => new PermissionDto
        {
            Id = p.Id,
            Code = p.Code,
            DisplayName = p.DisplayName,
            Module = p.Module
        }).ToList();
    }

    public async Task<List<RoleDto>> GetAllRolesAsync()
    {
        var roles = await _roleManager.Roles.ToListAsync();
        var result = new List<RoleDto>();

        foreach (var role in roles)
        {
            var claims = await _roleManager.GetClaimsAsync(role);
            result.Add(new RoleDto
            {
                Id = role.Id,
                Name = role.Name!,
                PermissionCodes = claims
                    .Where(c => c.Type == PermissionClaimType)
                    .Select(c => c.Value)
                    .ToList()
            });
        }

        return result;
    }

    public async Task<RoleDto?> GetRoleByIdAsync(string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role == null) return null;

        var claims = await _roleManager.GetClaimsAsync(role);
        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name!,
            PermissionCodes = claims.Where(c => c.Type == PermissionClaimType)
                                    .Select(c => c.Value).ToList()
        };
    }

    public async Task<(bool Success, string? Error)> CreateOrUpdateRoleAsync(CreateOrUpdateRoleDto dto)
    {
        IdentityRole role;

        if (string.IsNullOrEmpty(dto.Id))
        {
            // Guard against collision with the fixed login roles
            if (dto.Name is "Admin" or "Student" or "Staff")
                return (false, "This name is reserved for a system role.");

            if (await _roleManager.RoleExistsAsync(dto.Name))
                return (false, "A role with this name already exists.");

            role = new IdentityRole(dto.Name);
            var createResult = await _roleManager.CreateAsync(role);
            if (!createResult.Succeeded)
                return (false, string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }
        else
        {
            var existing = await _roleManager.FindByIdAsync(dto.Id);
            if (existing == null) return (false, "Role not found.");
            role = existing;

            if (role.Name != dto.Name)
            {
                role.Name = dto.Name;
                await _roleManager.UpdateAsync(role);
            }
        }

        // Sync claims: remove ones no longer selected, add newly selected ones
        var currentClaims = await _roleManager.GetClaimsAsync(role);
        var currentCodes = currentClaims.Where(c => c.Type == PermissionClaimType)
                                         .Select(c => c.Value).ToHashSet();

        foreach (var claim in currentClaims.Where(c => c.Type == PermissionClaimType
                                                        && !dto.SelectedPermissionCodes.Contains(c.Value)))
        {
            await _roleManager.RemoveClaimAsync(role, claim);
        }

        foreach (var code in dto.SelectedPermissionCodes.Where(c => !currentCodes.Contains(c)))
        {
            await _roleManager.AddClaimAsync(role, new Claim(PermissionClaimType, code));
        }

        return (true, null);
    }

    public async Task<bool> DeleteRoleAsync(string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role == null) return false;

        var result = await _roleManager.DeleteAsync(role);
        return result.Succeeded;
    }

    public async Task<List<StaffUserDto>> GetAllStaffUsersAsync()
    {
        var staffUsers = await _userManager.Users
            .Where(u => u.Role == UserRole.Staff)
            .ToListAsync();

        var result = new List<StaffUserDto>();
        foreach (var user in staffUsers)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new StaffUserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                AssignedRoleNames = roles.ToList()
            });
        }

        return result;
    }

    public async Task<(bool Success, string? Error)> AssignRoleToStaffAsync(AssignRoleToStaffDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.StaffUserId);
        if (user == null) return (false, "Staff member not found.");

        var role = await _roleManager.FindByIdAsync(dto.RoleId);
        if (role == null) return (false, "Role not found.");

        if (await _userManager.IsInRoleAsync(user, role.Name!))
            return (true, null); // already assigned, nothing to do

        var result = await _userManager.AddToRoleAsync(user, role.Name!);
        return result.Succeeded
            ? (true, null)
            : (false, string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    public async Task<(bool Success, string? Error)> RemoveRoleFromStaffAsync(AssignRoleToStaffDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.StaffUserId);
        if (user == null) return (false, "Staff member not found.");

        var role = await _roleManager.FindByIdAsync(dto.RoleId);
        if (role == null) return (false, "Role not found.");

        var result = await _userManager.RemoveFromRoleAsync(user, role.Name!);
        return result.Succeeded
            ? (true, null)
            : (false, string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}