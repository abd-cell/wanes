using System.Text.Json;
using Wanes.Areas.Domain.Audit;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Audit;

[ScopedInjectable]
public class AuditService : IAuditService
{
    private readonly IUnitOfWork _uow;

    public AuditService(IUnitOfWork uow) => _uow = uow;

    public async Task LogAsync(string action, string? entityType = null, int? entityId = null,
        object? before = null, object? after = null)
    {
        var ctx = AppHttpContext.Current;
        int? actor = null;
        if (int.TryParse(ctx?.User.FindFirst(AppClaims.UserId)?.Value, out var uid))
            actor = uid;

        var log = new AuditLog
        {
            ActorUserId = actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            Ip = ctx?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ctx?.Request.Headers.UserAgent.ToString(),
        };

        await _uow.Repository<AuditLog>().AddAsync(log);
        await _uow.SaveAsync();
    }
}
