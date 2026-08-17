using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.DTOs;

public class UpdateStatusRequest
{
    public ComplaintStatus Status { get; set; }
}
