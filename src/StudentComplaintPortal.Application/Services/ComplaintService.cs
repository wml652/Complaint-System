using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Exceptions;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;
using StudentComplaintPortal.Application.ServiceHelper;

namespace StudentComplaintPortal.Application.Services;

public class ComplaintService : IComplaintService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public ComplaintService(IUnitOfWork unitOfWork, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<ComplaintDto> CreateComplaintAsync(string studentId, CreateComplaintDto dto)
    {
        var complaint = new Complaint
        {
            Title = dto.Title,
            Description = dto.Description,
            Category = dto.Category,
            Status = ComplaintStatus.Open,
            StudentId = studentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Complaints.AddAsync(complaint);
        await _unitOfWork.SaveChangesAsync();

        // Reload with student info
        var created = await _unitOfWork.Complaints.GetByIdAsync(complaint.Id);
        return MapToDto(created!);
    }

    public async Task<ComplaintDto?> GetByIdAsync(int id)
    {
        var complaint = await _unitOfWork.Complaints.GetByIdAsync(id);
        return complaint == null ? null : MapToDto(complaint);
    }

    public async Task<IEnumerable<ComplaintDto>> GetByStudentAsync(string studentId)
    {
        var complaints = await _unitOfWork.Complaints.GetByStudentIdAsync(studentId);
        return complaints.Select(MapToDto);
    }

    public async Task<IEnumerable<ComplaintDto>> GetAllAsync()
    {
        var complaints = await _unitOfWork.Complaints.GetAllAsync();
        return complaints.Select(MapToDto);
    }

    public async Task<IEnumerable<ComplaintDto>> GetAssignedComplaintsAsync(string staffUserId)
    {
        // Uses the dedicated repository method so Student/Category are eager-loaded correctly
        var complaints = await _unitOfWork.Complaints.GetAssignedToStaffAsync(staffUserId);
        return complaints.Select(MapToDto);
    }

    public async Task<ComplaintDto> UpdateStatusAsync(int id, ComplaintStatus newStatus)
    {
        var complaint = await _unitOfWork.Complaints.GetByIdAsync(id);
        
        if (complaint == null)
        {
            throw new NotFoundException($"Complaint with ID {id} not found.");
        }

        // Validate status transition
        if (!IsValidStatusTransition(complaint.Status, newStatus))
        {
            throw new InvalidStatusTransitionException(
                $"Cannot transition from {complaint.Status} to {newStatus}.");
        }

        var oldStatus = complaint.Status;
        complaint.Status = newStatus;
        complaint.UpdatedAt = DateTime.UtcNow;
        
        _unitOfWork.Complaints.Update(complaint);
        await _unitOfWork.SaveChangesAsync();

        // Notify the student of the status change
        await _notificationService.NotifyAsync(
            complaint.StudentId,
            $"Your complaint '{complaint.Title}' status changed from {oldStatus} to {newStatus}",
            NotificationType.StatusChanged
        );

        return MapToDto(complaint);
    }

    private bool IsValidStatusTransition(ComplaintStatus currentStatus, ComplaintStatus newStatus)
    {
        // Team decision (update): a Closed complaint CAN be moved to any other
        // status again - status changes are no longer one-way.
        return true;
    }

    public async Task<CursorResult<ComplaintDto>> GetByStudentPagedAsync(string studentId, string? cursor, int pageSize = 20, bool moveForward = true)
        => await BuildChatListPageAsync((ct, ps, mf) => _unitOfWork.Complaints.GetByStudentIdPagedAsync(studentId, ct, ps, mf), cursor, pageSize, moveForward);

    public async Task<CursorResult<ComplaintDto>> GetAllPagedAsync(string? cursor, int pageSize = 20, bool moveForward = true)
        => await BuildChatListPageAsync((ct, ps, mf) => _unitOfWork.Complaints.GetAllPagedAsync(ct, ps, mf), cursor, pageSize, moveForward);

    public async Task<CursorResult<ComplaintDto>> GetAssignedComplaintsPagedAsync(string staffUserId, string? cursor, int pageSize = 20, bool moveForward = true)
        => await BuildChatListPageAsync((ct, ps, mf) => _unitOfWork.Complaints.GetAssignedToStaffPagedAsync(staffUserId, ct, ps, mf), cursor, pageSize, moveForward);

    private async Task<CursorResult<ComplaintDto>> BuildChatListPageAsync(
        Func<DateTime?, int, bool, Task<List<Complaint>>> fetchPage,
        string? cursor, int pageSize, bool moveForward)
    {
        if (pageSize < 1) pageSize = 10;
        var cursorTimestamp = PaginationHelper.DecodeTimestampCursor(cursor);

        var complaints = await fetchPage(cursorTimestamp, pageSize, moveForward);

        var hasMore = complaints.Count > pageSize;
        if (hasMore) complaints = complaints.Take(pageSize).ToList();

        var dtos = complaints.Select(MapToDto).ToList();

        string? nextCursor = hasMore ? PaginationHelper.EncodeTimestampCursor(complaints.Last().LastMessageAt ?? complaints.Last().CreatedAt) : null;
        string? previousCursor = complaints.Count > 0 ? PaginationHelper.EncodeTimestampCursor(complaints.First().LastMessageAt ?? complaints.First().CreatedAt) : null;

        return new CursorResult<ComplaintDto>
        {
            Items = dtos,
            NextCursor = nextCursor,
            PreviousCursor = previousCursor,
            HasMore = hasMore,
            PageSize = pageSize
        };
    }

    private ComplaintDto MapToDto(Complaint complaint)
    {
        return new ComplaintDto
        {
            Id = complaint.Id,
            Title = complaint.Title,
            Description = complaint.Description,
            Category = complaint.Category.ToString(),
            Status = complaint.Status.ToString(),
            StudentId = complaint.StudentId,
            StudentName = complaint.Student?.FullName ?? "Unknown Student",
            CreatedAt = complaint.CreatedAt,
            UpdatedAt = complaint.UpdatedAt
        };
    }
}
