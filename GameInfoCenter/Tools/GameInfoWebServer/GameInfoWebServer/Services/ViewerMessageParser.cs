using GameInfo.Protocol;

namespace GameInfoWebServer.Services;

internal static class ViewerMessageParser
{
    public static bool TryParseSubscribe(byte[] frame, out string sessionId, out string viewerToken)
    {
        sessionId = string.Empty;
        viewerToken = string.Empty;
        var span = frame.AsSpan();
        if (!ProtocolHeader.TryRead(span, out var header, out var hLen) || header.Kind != MessageKind.ViewerSubscribe)
        {
            return false;
        }

        var payload = span.Slice(hLen, header.PayloadLength);
        return ViewerSubscribePayload.TryRead(payload, out sessionId, out viewerToken, out _);
    }

    public static bool TryParseInspectorRequest(byte[] frame, out int instanceId)
    {
        instanceId = 0;
        var msg = frame.AsSpan();
        if (!ProtocolHeader.TryRead(msg, out var h2, out var hl2))
        {
            return false;
        }

        var pl = msg.Slice(hl2, h2.PayloadLength);
        if (h2.Kind != MessageKind.InspectorRequest)
        {
            return false;
        }

        return InspectorRequestPayload.TryRead(pl, out instanceId, out _);
    }
}
