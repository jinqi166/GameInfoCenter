namespace GameInfoWebServer.Models;

public sealed class CreateSessionResponse
{
    public required string SessionId { get; init; }

    public required string IngestToken { get; init; }

    public required string ViewerToken { get; init; }

    public required string IngestWebSocketPath { get; init; }

    public required string ViewerWebSocketPath { get; init; }

    public required string LivePagePath { get; init; }

    public int RecommendedHeartbeatIntervalMs { get; init; } = 5000;
}

public sealed class HeartbeatRequest
{
    public ulong Sequence { get; set; }

    public long ClientUnixTimeMs { get; set; }
}
