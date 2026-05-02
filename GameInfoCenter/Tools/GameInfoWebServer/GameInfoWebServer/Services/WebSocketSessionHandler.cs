using System.Collections.Concurrent;
using System.Net.WebSockets;
using GameInfo.Protocol;

namespace GameInfoWebServer.Services;

public static class WebSocketSessionHandler
{
    private static readonly ConcurrentDictionary<uint, WebSocket> InspectorWaiters = new();

    public static async Task HandleIngestAsync(WebSocket socket, GameSession session, CancellationToken cancellationToken)
    {
        session.IngestSocket = socket;
        var scratch = new byte[1024 * 1024];

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var frame = await WebSocketFrameReader.ReadBinaryFrameAsync(socket, scratch, cancellationToken).ConfigureAwait(false);
                if (frame is null)
                {
                    break;
                }

                if (!IngestFrameProcessor.TryProcess(frame, session, InspectorWaiters, out var broadcast, out var inspectorWs, out var inspectorFrame))
                {
                    break;
                }

                if (inspectorWs is not null && inspectorFrame is not null && inspectorWs.State == WebSocketState.Open)
                {
                    var responseClone = (byte[])inspectorFrame.Clone();
                    await inspectorWs.SendAsync(responseClone, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
                }

                if (broadcast)
                {
                    await BroadcastToViewersAsync(session, frame, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (ReferenceEquals(session.IngestSocket, socket))
            {
                session.IngestSocket = null;
            }

            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    public static async Task HandleViewerAsync(WebSocket socket, GameSessionStore store, CancellationToken cancellationToken)
    {
        GameSession? session = null;
        var scratch = new byte[512 * 1024];

        try
        {
            var first = await WebSocketFrameReader.ReadBinaryFrameAsync(socket, scratch, cancellationToken).ConfigureAwait(false);
            if (first is null)
            {
                return;
            }

            if (!ViewerMessageParser.TryParseSubscribe(first, out var sessionId, out var viewerToken))
            {
                await CloseWithErrorAsync(socket, "Expected ViewerSubscribe").ConfigureAwait(false);
                return;
            }

            if (!store.TryGet(sessionId, out session) || session is null || session.ViewerToken != viewerToken)
            {
                await SendSubscribeAckAsync(socket, false, "Invalid session or token", cancellationToken).ConfigureAwait(false);
                return;
            }

            await SendSubscribeAckAsync(socket, true, "ok", cancellationToken).ConfigureAwait(false);

            lock (session.ViewersLock)
            {
                session.Viewers.Add(socket);
            }

            await PushSnapshotStateAsync(socket, session, cancellationToken).ConfigureAwait(false);

            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var frame = await WebSocketFrameReader.ReadBinaryFrameAsync(socket, scratch, cancellationToken).ConfigureAwait(false);
                if (frame is null)
                {
                    break;
                }

                if (!ViewerMessageParser.TryParseInspectorRequest(frame, out var instanceId))
                {
                    continue;
                }

                {
                    var ingest = session.IngestSocket;
                    if (ingest is null || ingest.State != WebSocketState.Open)
                    {
                        continue;
                    }

                    var requestId = unchecked((uint)Random.Shared.Next(1, int.MaxValue));
                    while (!InspectorWaiters.TryAdd(requestId, socket))
                    {
                        requestId = unchecked((uint)Random.Shared.Next(1, int.MaxValue));
                    }

                    var requestBuffer = InspectorRequestBuilder.Build(instanceId, requestId);
                    await ingest.SendAsync(requestBuffer, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (session is not null)
            {
                lock (session.ViewersLock)
                {
                    session.Viewers.Remove(socket);
                }
            }

            foreach (var kv in InspectorWaiters.ToArray())
            {
                if (ReferenceEquals(kv.Value, socket))
                {
                    InspectorWaiters.TryRemove(kv.Key, out _);
                }
            }

            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static async Task PushSnapshotStateAsync(WebSocket socket, GameSession session, CancellationToken cancellationToken)
    {
        var frame = SnapshotFrameBuilder.BuildLatestFrame(session);
        if (frame is not null)
        {
            await socket.SendAsync(frame, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
        }

        var hierarchyFrame = SnapshotFrameBuilder.BuildHierarchyFrame(session.LatestHierarchyPayload);
        if (hierarchyFrame is not null)
        {
            await socket.SendAsync(hierarchyFrame, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task BroadcastToViewersAsync(GameSession session, byte[] frame, CancellationToken cancellationToken)
    {
        List<WebSocket> copy;
        lock (session.ViewersLock)
        {
            copy = session.Viewers.ToList();
        }

        foreach (var viewer in copy)
        {
            if (viewer.State != WebSocketState.Open)
            {
                continue;
            }

            try
            {
                var clone = (byte[])frame.Clone();
                await viewer.SendAsync(clone, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // ignore per-viewer failures
            }
        }
    }

    private static async Task SendSubscribeAckAsync(WebSocket socket, bool ok, string message, CancellationToken cancellationToken)
    {
        var buf = SubscribeAckBuilder.Build(ok, message);
        await socket.SendAsync(buf, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CloseWithErrorAsync(WebSocket socket, string reason)
    {
        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(WebSocketCloseStatus.ProtocolError, reason, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
