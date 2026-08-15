using System.ComponentModel.DataAnnotations;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.DTOs;

public class CreateComplaintDto
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    public required string Title { get; set; }

    [Required(ErrorMessage = "Description is required")]
    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public required string Description { get; set; }

    [Required(ErrorMessage = "Please select a category")]
    public required string Category { get; set; }  // Changed from enum to string to accept category name
}