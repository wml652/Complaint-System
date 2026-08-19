namespace StudentComplaintPortal.Application.Exceptions;

public class UnauthorizedComplaintAccessException : Exception
{
    public UnauthorizedComplaintAccessException(string message) : base(message)
    {
    }
}
