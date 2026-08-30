using System.Text.Json;
using Wanes.Shareds.Models;

namespace Wanes.Shareds.Middlewares;

/// <summary>
/// Converts a thrown <see cref="AppException"/> into HTTP 200 with a failed
/// <see cref="BaseResponse"/> body; any other exception becomes HTTP 500.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await WriteAsync(context, StatusCodes.Status200OK,
                BaseResponse.Fail(ex.ErrorCode, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                BaseResponse.Fail(ErrorCode.UnknownError));
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, BaseResponse body)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = status;
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
