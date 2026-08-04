using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Data;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Web.Controllers.Api;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly AppDbContext _context;

    public CategoriesController(ICategoryService categoryService, AppDbContext context)
    {
        _categoryService = categoryService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllActiveCategories()
    {
        try
        {
            var categories = await _categoryService.GetAllActiveCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        try
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);

            if (category == null)
            {
                return NotFound(new { message = $"Category with ID {id} not found" });
            }

            return Ok(category);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var category = await _categoryService.CreateCategoryAsync(dto);
            return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id }, category);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPatch("{id}/toggle-active")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleActiveStatus(int id)
    {
        try
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound(new { message = $"Category with ID {id} not found" });
            }

            category.IsActive = !category.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Category status updated", isActive = category.IsActive });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("staff")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetStaffUsers()
    {
        try
        {
            var staffUsers = await _context.Users
                .Where(u => u.Role == UserRole.Staff)
                .Select(u => new { u.Id, u.FullName, u.Email })
                .ToListAsync();

            return Ok(staffUsers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CreateCategoryDto dto)
    {
        try
        {
            var category = await _context.Categories
                .Include(c => c.AttachmentRules)
                .Include(c => c.Assignees)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                return NotFound(new { message = $"Category with ID {id} not found" });
            }

            category.Name = dto.Name;
            category.Description = dto.Description;
            category.Icon = dto.Icon ?? category.Icon;
            category.Color = dto.Color ?? category.Color;

            _context.CategoryAttachmentRules.RemoveRange(category.AttachmentRules);
            await _context.SaveChangesAsync();

            foreach (var ruleDto in dto.AttachmentRules)
            {
                var rule = new CategoryAttachmentRule
                {
                    CategoryId = category.Id,
                    FileType = ruleDto.FileType,
                    MaxFileCount = ruleDto.MaxFileCount,
                    MaxFileSizeBytes = ruleDto.MaxFileSizeBytes,
                    IsRequired = ruleDto.IsRequired
                };
                category.AttachmentRules.Add(rule);
            }

            _context.CategoryAssignees.RemoveRange(category.Assignees);
            foreach (var assigneeId in dto.AssigneeIds)
            {
                category.Assignees.Add(new CategoryAssignee
                {
                    CategoryId = category.Id,
                    AppUserId = assigneeId
                });
            }

            _context.Categories.Update(category);
            await _context.SaveChangesAsync();

            var updatedCategory = await _categoryService.GetCategoryByIdAsync(id);
            return Ok(updatedCategory);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        try
        {
            var category = await _context.Categories
                .Include(c => c.AttachmentRules)
                .Include(c => c.Assignees)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                return NotFound(new { message = $"Category with ID {id} not found" });
            }

            _context.CategoryAttachmentRules.RemoveRange(category.AttachmentRules);
            _context.CategoryAssignees.RemoveRange(category.Assignees);
            _context.Categories.Remove(category);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Category deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
