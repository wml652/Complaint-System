using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using StudentComplaintPortal.Application.Exceptions;
using StudentComplaintPortal.Application.Services.FileStorage;
using StudentComplaintPortal.Domain.Enums;
using StudentComplaintPortal.Web.Services;
using Xunit;

namespace StudentComplaintPortal.UnitTests;

public class FileStorageServiceTests
{
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly Mock<ILogger<LocalDiskFileStorageService>> _mockLogger;
    private readonly LocalDiskFileStorageService _service;
    private readonly string _testWebRootPath;

    public FileStorageServiceTests()
    {
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockLogger = new Mock<ILogger<LocalDiskFileStorageService>>();
        
        // Set up a test web root path
        _testWebRootPath = Path.Combine(Path.GetTempPath(), "test-uploads-" + Guid.NewGuid());
        Directory.CreateDirectory(_testWebRootPath);
        
        _mockEnvironment.Setup(e => e.WebRootPath).Returns(_testWebRootPath);
        _service = new LocalDiskFileStorageService(_mockEnvironment.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task UploadAsync_ValidPhoto_ReturnsFileUrl()
    {
        // Arrange
        var fileName = "test.jpg";
        var content = "fake image content"u8.ToArray();
        var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        // Act
        var result = await _service.UploadAsync(formFile, FileType.Photo, 1);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("/uploads/1/", result);
        Assert.Contains(".jpg", result);
    }

    [Fact]
    public async Task UploadAsync_InvalidMimeType_ThrowsInvalidFileException()
    {
        // Arrange
        var fileName = "test.txt";
        var content = "fake text content"u8.ToArray();
        var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidFileException>(
            () => _service.UploadAsync(formFile, FileType.Photo, 1));
    }

    [Fact]
    public async Task UploadAsync_InvalidExtension_ThrowsInvalidFileException()
    {
        // Arrange
        var fileName = "test.txt";
        var content = "fake content"u8.ToArray();
        var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg" // MIME type is correct
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidFileException>(
            () => _service.UploadAsync(formFile, FileType.Photo, 1));
    }

    [Fact]
    public async Task UploadAsync_FileTooLarge_ThrowsInvalidFileException()
    {
        // Arrange
        var fileName = "test.jpg";
        var fileSize = 11 * 1024 * 1024; // 11 MB (exceeds 10 MB limit for photos)
        var content = new byte[fileSize];
        var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, fileSize, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidFileException>(
            () => _service.UploadAsync(formFile, FileType.Photo, 1));
        
        Assert.Contains("cannot exceed", exception.Message);
    }

    [Fact]
    public async Task UploadAsync_ValidVideo_ReturnsFileUrl()
    {
        // Arrange
        var fileName = "test.mp4";
        var content = "fake video content"u8.ToArray();
        var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "video/mp4"
        };

        // Act
        var result = await _service.UploadAsync(formFile, FileType.Video, 2);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("/uploads/2/", result);
        Assert.Contains(".mp4", result);
    }

    [Fact]
    public async Task UploadAsync_ValidVoiceNote_ReturnsFileUrl()
    {
        // Arrange
        var fileName = "test.mp3";
        var content = "fake audio content"u8.ToArray();
        var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "audio/mpeg"
        };

        // Act
        var result = await _service.UploadAsync(formFile, FileType.VoiceNote, 3);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("/uploads/3/", result);
        Assert.Contains(".mp3", result);
    }

    [Fact]
    public async Task UploadAsync_EmptyFile_ThrowsInvalidFileException()
    {
        // Arrange
        var fileName = "test.jpg";
        var stream = new MemoryStream(Array.Empty<byte>());
        var formFile = new FormFile(stream, 0, 0, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidFileException>(
            () => _service.UploadAsync(formFile, FileType.Photo, 1));
    }
}
