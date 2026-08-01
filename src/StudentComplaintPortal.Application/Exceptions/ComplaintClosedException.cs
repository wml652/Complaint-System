namespace StudentComplaintPortal.Application.Exceptions;

public class ComplaintClosedException : Exception
{
    public ComplaintClosedException(string message) : base(message)
    {
    }
}
