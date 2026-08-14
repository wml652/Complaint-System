using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Data;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _context;

    public CategoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllActiveCategoriesAsync()
    {
        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .Include(c => c.AttachmentRules)
            .Include(c => c.Assignees)
            .ToListAsync();

        return categories.Select(MapToDto);
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(int id)
    {
        var category = await _context.Categories
            .Include(c => c.AttachmentRules)
            .Include(c => c.Assignees)
            .FirstOrDefaultAsync(c => c.Id == id);

        return category == null ? null : MapToDto(category);
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto)
    {
        var category = new Category
        {
            Name = dto.Name,
            Description = dto.Description,
            Icon = dto.Icon ?? "📋",
            Color = dto.Color ?? "#0d6efd",
            IsActive = true
        };

        // Add attachment rules
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

        // Add assignees
        foreach (var assigneeId in dto.AssigneeIds)
        {
            var assignee = new CategoryAssignee
            {
                CategoryId = category.Id,
                AppUserId = assigneeId
            };
            category.Assignees.Add(assignee);
        }

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return MapToDto(category);
    }

    private CategoryDto MapToDto(Category category)
    {
        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            Icon = category.Icon ?? "📋",
            Color = category.Color ?? "#0d6efd",
            IsActive = category.IsActive,
            AttachmentRules = category.AttachmentRules.Select(r => new CategoryAttachmentRuleDto
            {
                Id = r.Id,
                FileType = r.FileType,
                MaxFileCount = r.MaxFileCount,
                MaxFileSizeBytes = r.MaxFileSizeBytes,
                IsRequired = r.IsRequired
            }).ToList(),
            AssigneeIds = category.Assignees.Select(a => a.AppUserId).ToList()
        };
    }

    public async Task<IEnumerable<CategoryDto>> GetCategoriesForStaffAsync(string staffUserId)
    {
        var categories = await _context.Categories
            .Where(c => c.Assignees.Any(a => a.AppUserId == staffUserId))
            .Include(c => c.AttachmentRules)
            .Include(c => c.Assignees)
            .ToListAsync();

        return categories.Select(MapToDto);
    }

    public async Task<IEnumerable<CategoryListItemDto>> GetActiveCategoriesForDropdownAsync()
    {
        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryListItemDto
            {
                Id = c.Id,
                Name = c.Name,
                Icon = c.Icon ?? "📋",
                Color = c.Color ?? "#0d6efd"
            })
            .ToListAsync();

        return categories;
    }

}
