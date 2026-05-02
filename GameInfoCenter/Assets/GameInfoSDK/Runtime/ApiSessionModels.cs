using System;

namespace GameInfo.Runtime
{
    [Serializable]
    public sealed class CreateSessionResponseDto
    {
        public string sessionId;

        public string ingestToken;

        public string viewerToken;

        public string ingestWebSocketPath;

        public string viewerWebSocketPath;

        public string livePagePath;

        public int recommendedHeartbeatIntervalMs;
    }
}
