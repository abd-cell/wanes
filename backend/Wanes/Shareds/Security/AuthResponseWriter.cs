using System.Text.Json;
using Wanes.Shareds.Models;

namespace Wanes.Shareds.Security;

/// <summary>
/// Writes a uniform <see cref="BaseResponse"/> body for the two authorization
/// rejections ASP.NET otherwise answers with an empty 401/403, so clients (CMS,
/// app) can show the reason instead of inferring it from the status code alone.
/// </summary>
public static class AuthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>No token, or a token that no longer maps to a live session.</summary>
    public static Task WriteUnauthorizedAsync(HttpContext context) =>
        WriteAsync(context, StatusCodes.Status401Unauthorized, ErrorCode.Unauthorized,
            IsArabic(context)
                ? "انتهت الجلسة. يرجى تسجيل الدخول مرة أخرى."
                : "Your session has expired. Please sign in again.");

    /// <summary>Authenticated, but missing the role the endpoint requires (Admin).</summary>
    public static Task WriteForbiddenAsync(HttpContext context) =>
        WriteAsync(context, StatusCodes.Status403Forbidden, ErrorCode.Forbidden,
            IsArabic(context)
                ? "ليس لديك صلاحية الوصول. هذه الصفحة تتطلب دور المدير."
                : "You do not have permission to access this resource. An Admin role is required.");

    private static async Task WriteAsync(HttpContext context, int status, ErrorCode code, string message)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new BaseResponse(code, message), JsonOptions));
    }

    // Clients send Accept-Language ("en" / "ar"); there is no localization
    // pipeline yet, so the two strings live here.
    private static bool IsArabic(HttpContext context) =>
        context.Request.Headers.AcceptLanguage.ToString()
            .StartsWith("ar", StringComparison.OrdinalIgnoreCase);
}
