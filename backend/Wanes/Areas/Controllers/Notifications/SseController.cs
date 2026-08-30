using Microsoft.AspNetCore.Mvc;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Security;
using Wanes.Shareds.SSE;

namespace Wanes.Areas.Controllers.Notifications;

/// <summary>Server-Sent-Events stream for the authenticated user (live notifications).</summary>
[AppAuthorize]
[Route("api/v1/sse")]
public class SseController : ControllerBase
{
    private readonly SseConnectionManager _sse;
    private readonly ISecurityManager _security;

    public SseController(SseConnectionManager sse, ISecurityManager security)
    {
        _sse = sse;
        _security = security;
    }

    [HttpGet]
    public async Task Stream(CancellationToken ct)
    {
        var userId = _security.RequireUserId();

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var channel = _sse.Connect(userId);
        try
        {
            await Response.WriteAsync(": connected\n\n", ct);
            await Response.Body.FlushAsync(ct);

            await foreach (var message in channel.Reader.ReadAllAsync(ct))
            {
                await Response.WriteAsync($"data: {message}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnected — expected
        }
        finally
        {
            _sse.Disconnect(userId, channel);
        }
    }
}
