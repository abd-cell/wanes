namespace Wanes.Shareds.Models;

/// <summary>
/// Domain exception carrying an <see cref="ErrorCode"/>. The exception middleware
/// converts it to an HTTP 200 with a failed <see cref="BaseResponse"/> body.
/// </summary>
public class AppException : Exception
{
    public ErrorCode ErrorCode { get; }

    public AppException(ErrorCode errorCode, string? message = null) : base(message)
    {
        ErrorCode = errorCode;
    }
}
