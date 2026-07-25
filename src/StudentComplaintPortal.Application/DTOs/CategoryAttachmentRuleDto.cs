namespace StudentComplaintPortal.Application.DTOs;

public class CategoryAttachmentRuleDto
{
    public int Id { get; set; }
    public string FileType { get; set; } = string.Empty;
    public int MaxFileCount { get; set; }
    public long MaxFileSizeBytes { get; set; }
    public bool IsRequired { get; set; }
}
