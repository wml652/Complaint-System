using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Domain.Enums;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Controllers.Mvc;

[Authorize]
public class ComplaintController : Controller
{
    private readonly IComplaintService _complaintService;
    private readonly IMessageService _messageService;

    public ComplaintController(IComplaintService complaintService, IMessageService messageService)
    {
        _complaintService = complaintService;
        _messageService = messageService;

    }

    [HttpGet]
    

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Permission:Complaints.ChangeStatus")]
    public async Task<IActionResult> UpdateStatus(int id, ComplaintStatus newStatus)
    {
        try
        {
            await _complaintService.UpdateStatusAsync(id, newStatus);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Ok(new { success = true });
            }

            TempData["SuccessMessage"] = "Complaint status updated successfully.";
        }
        catch (Exception ex)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
            TempData["ErrorMessage"] = $"Failed to update status: {ex.Message}";
        }

        return RedirectToAction("Detail", new { id });
    }
    [HttpGet]
    public async Task<IActionResult> GetMessages(int complaintId)
    {
        var messages = await _messageService.GetConversationAsync(complaintId);
        return Json(messages);
    }
    public async Task<IActionResult> GetMessagesPaged(int complaintId, string? cursor = null, int pageSize = 20, bool forward = true)
    {
        var result = await _messageService.GetConversationPagedAsync(complaintId, cursor, pageSize, forward);
        return Json(result);
    }
}
