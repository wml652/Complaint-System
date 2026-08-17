using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.Services.FileStorage;

public interface IFileStorageService
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, FileType fileType, int complaintId);
}
