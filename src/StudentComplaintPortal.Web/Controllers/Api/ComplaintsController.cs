using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Exceptions;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Domain.Enums;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Controllers.Api;

[ApiController]
[Route("api/v1/complaints")]
[Authorize]
public class ComplaintsController : ControllerBase
{
    private readonly IComplaintService _complaintService;

    public ComplaintsController(IComplaintService complaintService)
    {
        _complaintService = complaintService;
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var complaint = await _complaintService.CreateComplaintAsync(userId, dto);

        return CreatedAtAction(nameof(GetComplaint), new { id = complaint.Id }, complaint);
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyComplaints()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var complaints = await _complaintService.GetByStudentAsync(userId);
        return Ok(complaints);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllComplaints()
    {
        var complaints = await _complaintService.GetAllAsync();
        return Ok(complaints);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetComplaint(int id)
    {
        var complaint = await _complaintService.GetByIdAsync(id);
        
        if (complaint == null)
            return NotFound(new { message = $"Complaint with ID {id} not found" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role)!;

        // Students can only view their own complaints
        if (userRole == "Student" && complaint.StudentId != userId)
        {
            return Forbid();
        }

        return Ok(complaint);
    }

    [HttpGet("paged")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllComplaintsPaged(
    [FromQuery] int? categoryId,
    [FromQuery] ComplaintStatus? status,
    [FromQuery] bool unreadOnly = false,
    [FromQuery] DateTime? startDate = null,
    [FromQuery] DateTime? endDate = null,
    [FromQuery] string? cursor = null,
    [FromQuery] int pageSize = 20,
    [FromQuery] bool moveForward = true)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _complaintService.GetFilteredPagedAsync(categoryId, status, unreadOnly, userId, null, startDate, endDate, cursor, pageSize, moveForward);
        return Ok(result);
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var complaint = await _complaintService.UpdateStatusAsync(id, request.Status);
        return Ok(complaint);
    }
}
