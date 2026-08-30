namespace Wanes.Areas.Services.Audit;

public interface IAuditService
{
    /// <summary>Appends one immutable audit row. Never throws into the caller's flow.</summary>
    Task LogAsync(string action, string? entityType = null, int? entityId = null,
        object? before = null, object? after = null);
}
