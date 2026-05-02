using System.Collections.Concurrent;
using System.Net.WebSockets;
using GameInfo.Protocol;

namespace GameInfoWebServer.Services;

internal static class IngestFrameProcessor
{
    public static bool TryProcess(
        byte[] frame,
        GameSession session,
        ConcurrentDictionary<uint, WebSocket> inspectorWaiters,
        out bool broadcastToViewers,
        out WebSocket? inspectorTarget,
        out byte[]? inspectorFrame)
    {
        broadcastToViewers = false;
        inspectorTarget = null;
        inspectorFrame = null;

        var span = frame.AsSpan();
        if (!ProtocolHeader.TryRead(span, out var header, out var headerLen))
        {
            return false;
        }

        var payload = span.Slice(headerLen, header.PayloadLength);
        if (payload.Length != header.PayloadLength)
        {
            return false;
        }

        switch (header.Kind)
        {
            case MessageKind.SessionHello:
                return true;
            case MessageKind.FrameJpeg:
                if (FrameJpegPayload.TryRead(payload, out var w, out var h, out var frameIdx, out var jpeg, out _))
                {
                    session.UpdateFrame(w, h, frameIdx, jpeg);
                    broadcastToViewers = true;
                }

                return true;
            case MessageKind.HierarchySnapshot:
                session.UpdateHierarchy(payload);
                broadcastToViewers = true;
                return true;
            case MessageKind.InspectorResponse:
                if (inspectorWaiters.TryRemove(header.RequestId, out var waiter))
                {
                    inspectorTarget = waiter;
                    inspectorFrame = frame;
                }

                return true;
            default:
                return true;
        }
    }
}
