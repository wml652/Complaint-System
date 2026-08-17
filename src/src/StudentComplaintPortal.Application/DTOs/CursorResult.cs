namespace StudentComplaintPortal.Application.DTOs;

public class CursorResult<T>
{
    public List<T> Items { get; set; } = new();
    public string? NextCursor { get; set; }
    public string? PreviousCursor { get; set; }
    public bool HasMore { get; set; }
    public int PageSize { get; set; }
}