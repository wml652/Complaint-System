using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;

namespace StudentComplaintPortal.Web.Controllers.Mvc;

[Authorize(Roles = "Admin")]
public class RoleManagementController : Controller
{
    private readonly IRoleManagementService _roleService;

    public RoleManagementController(IRoleManagementService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var roles = await _roleService.GetAllRolesAsync();
        return View(roles);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string? id)
    {
        var permissions = await _roleService.GetAllPermissionsAsync();

        if (!string.IsNullOrEmpty(id))
        {
            var role = await _roleService.GetRoleByIdAsync(id);
            if (role == null) return NotFound();

            foreach (var perm in permissions)
                perm.IsGranted = role.PermissionCodes.Contains(perm.Code);

            ViewBag.RoleId = role.Id;
            ViewBag.RoleName = role.Name;
        }

        return View(permissions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CreateOrUpdateRoleDto dto)
    {
        var (success, error) = await _roleService.CreateOrUpdateRoleAsync(dto);
        if (!success)
        {
            ModelState.AddModelError("", error!);
            var permissions = await _roleService.GetAllPermissionsAsync();
            return View(permissions);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        await _roleService.DeleteRoleAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> AssignToStaff()
    {
        var staff = await _roleService.GetAllStaffUsersAsync();
        var roles = await _roleService.GetAllRolesAsync();
        ViewBag.Roles = roles;
        return View(staff);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignToStaff(AssignRoleToStaffDto dto)
    {
        await _roleService.AssignRoleToStaffAsync(dto);
        return RedirectToAction(nameof(AssignToStaff));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromStaff(AssignRoleToStaffDto dto)
    {
        await _roleService.RemoveRoleFromStaffAsync(dto);
        return RedirectToAction(nameof(AssignToStaff));
    }
}
