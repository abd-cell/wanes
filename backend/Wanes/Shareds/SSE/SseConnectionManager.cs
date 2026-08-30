using System.Collections.Concurrent;
using System.Threading.Channels;
using Wanes.Shareds.Attributes;

namespace Wanes.Shareds.SSE;

/// <summary>
/// Tracks live Server-Sent-Events connections per user. Each connection owns an
/// unbounded channel; <see cref="SendAsync"/> fans a message out to every open
/// connection for that user. Singleton — shared across the app.
/// </summary>
[SingletonInjectable]
public class SseConnectionManager
{
    private readonly ConcurrentDictionary<int, List<Channel<string>>> _clients = new();

    public Channel<string> Connect(int userId)
    {
        var channel = Channel.CreateUnbounded<string>();
        var list = _clients.GetOrAdd(userId, _ => []);
        lock (list) list.Add(channel);
        return channel;
    }

    public void Disconnect(int userId, Channel<string> channel)
    {
        if (!_clients.TryGetValue(userId, out var list)) return;
        lock (list)
        {
            list.Remove(channel);
            if (list.Count == 0) _clients.TryRemove(userId, out _);
        }
    }

    public async Task SendAsync(int userId, string data)
    {
        if (!_clients.TryGetValue(userId, out var list)) return;
        List<Channel<string>> snapshot;
        lock (list) snapshot = list.ToList();
        foreach (var channel in snapshot)
            await channel.Writer.WriteAsync(data);
    }
}
