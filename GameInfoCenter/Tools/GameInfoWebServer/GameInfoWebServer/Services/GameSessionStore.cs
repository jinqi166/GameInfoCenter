using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace GameInfoWebServer.Services;

public sealed class GameSessionStore
{
    private readonly ConcurrentDictionary<string, GameSession> _sessions = new(StringComparer.Ordinal);

    public GameSession CreateSession()
    {
        var sessionId = NewToken(12);
        var ingestToken = NewToken(24);
        var viewerToken = NewToken(24);
        var session = new GameSession(sessionId, ingestToken, viewerToken);
        if (!_sessions.TryAdd(sessionId, session))
        {
            return CreateSession();
        }

        return session;
    }

    public bool TryGet(string sessionId, out GameSession? session)
    {
        return _sessions.TryGetValue(sessionId, out session);
    }

    public void Remove(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }

    private static string NewToken(int byteLength)
    {
        var bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }
}
