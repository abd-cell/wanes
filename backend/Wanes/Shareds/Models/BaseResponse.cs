namespace Wanes.Shareds.Models;

/// <summary>
/// Uniform response envelope. Construct directly:
/// <c>new BaseResponse()</c> for success, <c>new BaseResponse(ErrorCode.NotFound)</c>
/// for errors; the generic form carries data.
/// </summary>
public class BaseResponse
{
    public bool Success { get; set; }
    public ErrorCode ErrorCode { get; set; } = ErrorCode.Success;
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = [];

    public BaseResponse()
    {
        Success = true;
        ErrorCode = ErrorCode.Success;
    }

    public BaseResponse(ErrorCode errorCode, string? message = null)
    {
        Success = false;
        ErrorCode = errorCode;
        Message = message;
    }

    // Back-compat factory helpers (kept so existing call sites keep working).
    public static BaseResponse Ok(string? message = null) => new() { Message = message };
    public static BaseResponse Fail(ErrorCode code, string? message = null) => new(code, message);
}

public class BaseResponse<T> : BaseResponse
{
    public T? Data { get; set; }

    public BaseResponse() : base() { }

    public BaseResponse(T data) : base() => Data = data;

    public BaseResponse(T? data, ErrorCode errorCode, string? message = null)
        : base(errorCode, message) => Data = data;

    public static BaseResponse<T> Ok(T data, string? message = null) =>
        new(data) { Message = message };

    public static new BaseResponse<T> Fail(ErrorCode code, string? message = null) =>
        new(default, code, message);
}
