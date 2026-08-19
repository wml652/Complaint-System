using StudentComplaintPortal.Application.DTOs;

namespace StudentComplaintPortal.Application.Services;

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllActiveCategoriesAsync();
    Task<CategoryDto?> GetCategoryByIdAsync(int id);
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto);
    Task<IEnumerable<CategoryDto>> GetCategoriesForStaffAsync(string staffUserId);

    Task<IEnumerable<CategoryListItemDto>> GetActiveCategoriesForDropdownAsync();
}
