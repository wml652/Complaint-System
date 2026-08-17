using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllActiveCategoriesAsync()
    {
        var categories = await _unitOfWork.Categories.GetAllActiveWithDetailsAsync();
        return categories.Select(MapToDto);
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(int id)
    {
        var category = await _unitOfWork.Categories.GetByIdWithDetailsAsync(id);
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

        foreach (var assigneeId in dto.AssigneeIds)
        {
            var assignee = new CategoryAssignee
            {
                CategoryId = category.Id,
                AppUserId = assigneeId
            };
            category.Assignees.Add(assignee);
        }

        await _unitOfWork.Categories.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(category);
    }

    public async Task<IEnumerable<CategoryDto>> GetCategoriesForStaffAsync(string staffUserId)
    {
        var categories = await _unitOfWork.Categories.GetAssignedToStaffAsync(staffUserId);
        return categories.Select(MapToDto);
    }

    // FIX 1: Fetch dynamic categories for the dropdown
    public async Task<IEnumerable<CategoryListItemDto>> GetActiveCategoriesForDropdownAsync()
    {
        // Use the Repository instead of the DbContext directly
        var categories = await _unitOfWork.Categories.GetAllActiveWithDetailsAsync();

        return categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryListItemDto
            {
                Id = c.Id,
                Name = c.Name,
                Icon = c.Icon ?? "📋",
                Color = c.Color ?? "#0d6efd"
            })
            .ToList();
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
}