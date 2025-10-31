namespace MiGenteEnLinea.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when a request is invalid or cannot be processed due to business logic constraints
/// Maps to HTTP 400 Bad Request
/// </summary>
public class BadRequestException : Exception
{
    public BadRequestException()
        : base()
    {
    }

    public BadRequestException(string message)
        : base(message)
    {
    }

    public BadRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
