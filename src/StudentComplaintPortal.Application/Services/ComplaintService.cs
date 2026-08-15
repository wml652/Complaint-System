using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Exceptions;
using StudentComplaintPortal.Application.ServiceHelper;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

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
        // Map category name string to enum
        ComplaintCategory categoryEnum;
        if (!Enum.TryParse<ComplaintCategory>(dto.Category, ignoreCase: true, out categoryEnum))
        {
            categoryEnum = ComplaintCategory.Other;
        }

        // NEW: Fetch active categories via repository and find the matching ID
        var activeCategories = await _unitOfWork.Categories.GetAllActiveWithDetailsAsync();
        var categoryEntity = activeCategories.FirstOrDefault(c =>
            c.Name.Equals(dto.Category, StringComparison.OrdinalIgnoreCase));

        var complaint = new Complaint
        {
            Title = dto.Title,
            Description = dto.Description,
            Category = categoryEnum,
            CategoryId = categoryEntity?.Id,
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

    public async Task<CursorResult<ComplaintDto>> GetFilteredPagedAsync(int? categoryId, ComplaintStatus? status, bool unreadOnly, string? currentUserId, string? staffScopeUserId, DateTime? startDate, DateTime? endDate, string? cursor, int pageSize = 20, bool moveForward = true)
    => await BuildChatListPageAsync((ct, ps, mf) => _unitOfWork.Complaints.GetFilteredPagedAsync(categoryId, status, unreadOnly, currentUserId, staffScopeUserId, startDate, endDate, ct, ps, mf), cursor, pageSize, moveForward);

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

    public async Task<List<CampaignSummaryDto>> GetCampaignSummariesAsync(string? staffScopeUserId)
    {
        var complaints = string.IsNullOrEmpty(staffScopeUserId)
            ? await _unitOfWork.Complaints.GetAllAsync()
            : await _unitOfWork.Complaints.GetAssignedToStaffAsync(staffScopeUserId);

        var grouped = complaints
            .Select(c => new { Complaint = c, Key = GetCampaignKey(c.CreatedAt) })
            .GroupBy(x => x.Key)
            .Select(g => new CampaignSummaryDto
            {
                Semester = g.Key.Semester,
                Year = g.Key.Year,
                StartDate = g.Key.StartDate,
                EndDate = g.Key.EndDate,
                Total = g.Count(),
                Open = g.Count(x => x.Complaint.Status == ComplaintStatus.Open),
                InProgress = g.Count(x => x.Complaint.Status == ComplaintStatus.InProgress),
                Resolved = g.Count(x => x.Complaint.Status == ComplaintStatus.Resolved),
                Closed = g.Count(x => x.Complaint.Status == ComplaintStatus.Closed)
            })
            .OrderByDescending(c => c.StartDate)
            .ToList();

        return grouped;
    }

    // Semester boundaries:
    //   Spring: Feb 1 - Jun 30
    //   Summer: Jul 1 - Aug 31
    //   Fall:   Sep 1 - Jan 31 (crosses calendar year - Jan belongs to PREVIOUS year's Fall)
    // "Year" yahan hamesha semester ke SHURU hone wale saal ko refer karta hai,
    // isliye Fall 2026 = Sep 2026 - Jan 2027, chahe January ka CreatedAt.Year 2027 ho.
    private static (string Semester, int Year, DateTime StartDate, DateTime EndDate) GetCampaignKey(DateTime createdAt)
    {
        var month = createdAt.Month;
        var year = createdAt.Year;

        if (month == 1) // January -> previous year's Fall
        {
            var fallYear = year - 1;
            return ("Fall", fallYear, new DateTime(fallYear, 9, 1), new DateTime(fallYear + 1, 1, 31));
        }
        if (month is >= 2 and <= 6) // Feb-Jun -> Spring
        {
            return ("Spring", year, new DateTime(year, 2, 1), new DateTime(year, 6, 30));
        }
        if (month is 7 or 8) // Jul-Aug -> Summer
        {
            return ("Summer", year, new DateTime(year, 7, 1), new DateTime(year, 8, 31));
        }
        // Sep-Dec -> Fall (same year it started)
        return ("Fall", year, new DateTime(year, 9, 1), new DateTime(year + 1, 1, 31));
    }
}