using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Domain.Entities;

public class CategoryAttachmentRule
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public FileType FileType { get; set; }
    public int MaxFileCount { get; set; }
    public long MaxFileSizeBytes { get; set; }
    public bool IsRequired { get; set; }

    // Navigation property
    public Category Category { get; set; } = null!;
}
