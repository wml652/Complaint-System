using Microsoft.AspNetCore.Http;
using StudentComplaintPortal.Application.Exceptions;
using StudentComplaintPortal.Application.Services.FileStorage;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Web.Services;

public class LocalDiskFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LocalDiskFileStorageService> _logger;

    private static readonly Dictionary<FileType, (string[] AllowedMimeTypes, string[] AllowedExtensions, long MaxSizeBytes)> ValidationRules = new()
    {
        [FileType.Photo] = (
            new[] { "image/jpeg", "image/png", "image/webp" },
            new[] { ".jpg", ".jpeg", ".png", ".webp" },
            10 * 1024 * 1024 // 10 MB
        ),
        [FileType.Video] = (
            new[] { "video/mp4", "video/webm" },
            new[] { ".mp4", ".webm" },
            100 * 1024 * 1024 // 100 MB
        ),
        [FileType.VoiceNote] = (
            new[] { "audio/mpeg", "audio/webm", "audio/wav" },
            new[] { ".mp3", ".webm", ".wav" },
            20 * 1024 * 1024 // 20 MB
        )
    };

    public LocalDiskFileStorageService(IWebHostEnvironment environment, ILogger<LocalDiskFileStorageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> UploadAsync(IFormFile file, FileType fileType, int complaintId)
    {
        if (file == null || file.Length == 0)
        {
            throw new InvalidFileException("File is required and cannot be empty.");
        }

        using var stream = file.OpenReadStream();
        return await UploadAsync(stream, file.FileName, file.ContentType, fileType, complaintId);
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, FileType fileType, int complaintId)
    {
        // Validate file is not null
        if (fileStream == null || fileStream.Length == 0)
        {
            throw new InvalidFileException("File is required and cannot be empty.");
        }

        // Get validation rules for the file type
        if (!ValidationRules.TryGetValue(fileType, out var rules))
        {
            throw new InvalidFileException($"Unsupported file type: {fileType}");
        }

        // Validate file size
        if (fileStream.Length > rules.MaxSizeBytes)
        {
            var maxSizeMB = rules.MaxSizeBytes / (1024.0 * 1024.0);
            throw new InvalidFileException($"{fileType} files cannot exceed {maxSizeMB} MB. Your file is {fileStream.Length / (1024.0 * 1024.0):F2} MB.");
        }

        // Validate MIME type
        if (!rules.AllowedMimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidFileException($"Invalid MIME type '{contentType}' for {fileType}. Allowed types: {string.Join(", ", rules.AllowedMimeTypes)}");
        }

        // Validate file extension
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!rules.AllowedExtensions.Contains(extension))
        {
            throw new InvalidFileException($"Invalid file extension '{extension}' for {fileType}. Allowed extensions: {string.Join(", ", rules.AllowedExtensions)}");
        }

        // Create directory structure: wwwroot/uploads/{complaintId}/
        var complaintFolder = Path.Combine(_environment.WebRootPath, "uploads", complaintId.ToString());
        Directory.CreateDirectory(complaintFolder);

        // Generate unique filename: {guid}-{originalFileName}
        var uniqueFileName = $"{Guid.NewGuid()}-{fileName}";
        var filePath = Path.Combine(complaintFolder, uniqueFileName);

        // Save file to disk
        try
        {
            using var diskStream = new FileStream(filePath, FileMode.Create);
            await fileStream.CopyToAsync(diskStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file {FileName} to {FilePath}", fileName, filePath);
            throw new InvalidFileException("Failed to save file to disk.");
        }

        // Return relative URL path
        return $"/uploads/{complaintId}/{uniqueFileName}";
    }
}
