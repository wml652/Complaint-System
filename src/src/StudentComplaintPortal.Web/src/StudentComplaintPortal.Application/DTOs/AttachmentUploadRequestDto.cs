using Microsoft.AspNetCore.Http;

namespace StudentComplaintPortal.Application.DTOs;

public class AttachmentUploadRequestDto
{
    public IFormFile File { get; set; } = null!;
    public string FileType { get; set; } = string.Empty;
    public string? Content { get; set; }
}
