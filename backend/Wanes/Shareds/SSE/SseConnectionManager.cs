using System.Collections.Concurrent;
using System.Threading.Channels;
using Wanes.Shareds.Attributes;

namespace Wanes.Shareds.SSE;

/// <summary>
/// Tracks live Server-Sent-Events connections per user. Each connection owns an
/// unbounded channel; <see cref="SendAsync"/> fans a message out to every open
/// connection for that user. Singleton — shared across the app.
///
/// Connections carry the session key they were opened with as well as the user
/// id. The stream authenticates once, at connect, and then lives for as long as
/// the socket does — so without the session key there is no way to end the one
/// stream belonging to a session that has just been revoked, and a signed-out
/// device keeps receiving the account's notifications. See
/// <see cref="DisconnectSession"/>.
/// </summary>
[SingletonInjectable]
public class SseConnectionManager
{
    private sealed record Connection(string SessionKey, Channel<string> Channel);

    private readonly ConcurrentDictionary<int, List<Connection>> _clients = new();

    public Channel<string> Connect(int userId, string sessionKey)
    {
        var channel = Channel.CreateUnbounded<string>();
        var list = _clients.GetOrAdd(userId, _ => []);
        lock (list) list.Add(new Connection(sessionKey, channel));
        return channel;
    }

    public void Disconnect(int userId, Channel<string> channel)
    {
        if (!_clients.TryGetValue(userId, out var list)) return;
        lock (list)
        {
            list.RemoveAll(c => c.Channel == channel);
            if (list.Count == 0) _clients.TryRemove(userId, out _);
        }
    }

    /// <summary>
    /// Ends the streams belonging to one session — the device that just signed
    /// out, or whose session was revoked.
    ///
    /// Completing the writer ends the controller's read loop, which closes the
    /// response; the client sees the stream finish and, finding no token, does
    /// not reconnect. Scoped to the session on purpose: the same account signed
    /// in on another handset keeps its stream.
    /// </summary>
    public void DisconnectSession(int userId, string sessionKey)
    {
        if (string.IsNullOrEmpty(sessionKey)) return;
        if (!_clients.TryGetValue(userId, out var list)) return;

        List<Connection> going;
        lock (list)
        {
            going = list.Where(c => c.SessionKey == sessionKey).ToList();
            list.RemoveAll(c => c.SessionKey == sessionKey);
            if (list.Count == 0) _clients.TryRemove(userId, out _);
        }

        foreach (var connection in going) connection.Channel.Writer.TryComplete();
    }

    public async Task SendAsync(int userId, string data)
    {
        if (!_clients.TryGetValue(userId, out var list)) return;
        List<Connection> snapshot;
        lock (list) snapshot = list.ToList();
        foreach (var connection in snapshot)
            await Write(connection, data);
    }

    /// <summary>
    /// Sends one frame to every open connection, whoever it belongs to.
    ///
    /// Used for the small "this thing is no longer live" events (a hail that was
    /// cancelled, taken or expired) rather than for notifications, which always
    /// have a named recipient. Addressing those by recipient is not possible in
    /// any honest way: the audience was computed from where each driver was
    /// standing when the hail opened, and by the time it closes they have moved,
    /// gone offline or come online. The frame carries an id and nothing else, and
    /// a client that is not holding that id ignores it.
    /// </summary>
    public async Task BroadcastAsync(string data)
    {
        foreach (var list in _clients.Values)
        {
            List<Connection> snapshot;
            lock (list) snapshot = list.ToList();
            foreach (var connection in snapshot)
                await Write(connection, data);
        }
    }

    /// <summary>
    /// A completed channel belongs to a session that has just been revoked and
    /// is on its way out; writing to it throws. One dead connection must not
    /// cost the rest of the fan-out, so the write is dropped rather than raised.
    /// </summary>
    private static async Task Write(Connection connection, string data)
    {
        try
        {
            await connection.Channel.Writer.WriteAsync(data);
        }
        catch (ChannelClosedException)
        {
            // gone — Disconnect/DisconnectSession will have unlisted it already
        }
    }
}
