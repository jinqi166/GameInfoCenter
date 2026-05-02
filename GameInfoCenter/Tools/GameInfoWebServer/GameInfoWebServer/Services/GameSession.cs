using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace GameInfoWebServer.Services;

public sealed class GameSession
{
    private readonly object _frameLock = new();

    private byte[]? _latestJpeg;

    public string SessionId { get; }

    public string IngestToken { get; }

    public string ViewerToken { get; }

    public DateTimeOffset LastHeartbeatUtc { get; set; } = DateTimeOffset.UtcNow;

    public WebSocket? IngestSocket { get; set; }

    public int LatestWidth { get; private set; }

    public int LatestHeight { get; private set; }

    public ulong LatestFrameIndex { get; private set; }

    public byte[]? LatestHierarchyPayload { get; private set; }

    public ConcurrentQueue<int> PendingInspectorTargets { get; } = new();

    public readonly object ViewersLock = new();

    public List<WebSocket> Viewers { get; } = new();

    public GameSession(string sessionId, string ingestToken, string viewerToken)
    {
        SessionId = sessionId;
        IngestToken = ingestToken;
        ViewerToken = viewerToken;
    }

    public void UpdateFrame(int width, int height, ulong frameIndex, ReadOnlySpan<byte> jpeg)
    {
        var copy = new byte[jpeg.Length];
        jpeg.CopyTo(copy);
        lock (_frameLock)
        {
            _latestJpeg = copy;
            LatestWidth = width;
            LatestHeight = height;
            LatestFrameIndex = frameIndex;
        }
    }

    public bool TryGetLatestFrame(out int width, out int height, out ulong frameIndex, out byte[]? jpeg)
    {
        lock (_frameLock)
        {
            width = LatestWidth;
            height = LatestHeight;
            frameIndex = LatestFrameIndex;
            jpeg = _latestJpeg is null ? null : (byte[])_latestJpeg.Clone();
            return jpeg is not null;
        }
    }

    public void UpdateHierarchy(ReadOnlySpan<byte> payload)
    {
        var copy = new byte[payload.Length];
        payload.CopyTo(copy);
        LatestHierarchyPayload = copy;
    }
}
