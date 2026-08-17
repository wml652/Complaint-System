using System.ComponentModel.DataAnnotations;

namespace StudentComplaintPortal.Web.Models;

public class SendMessageRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}
