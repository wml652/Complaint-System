namespace StudentComplaintPortal.Application.DTOs;

// Ek "campaign" = ek semester. Koi Campaign table nahi - ye Complaint.CreatedAt se
// on-the-fly derive hota hai (Jan-Jun = Spring, Jul-Dec = Fall).
// Naya saal aate hi naya campaign khud ban jata hai - koi code change nahi chahiye.
public class CampaignSummaryDto
{
    public required string Semester { get; set; } // "Spring" or "Fall"
    public int Year { get; set; }
    public string Label => $"{Semester} {Year}";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Total { get; set; }
    public int Open { get; set; }
    public int InProgress { get; set; }
    public int Resolved { get; set; }
    public int Closed { get; set; }
}