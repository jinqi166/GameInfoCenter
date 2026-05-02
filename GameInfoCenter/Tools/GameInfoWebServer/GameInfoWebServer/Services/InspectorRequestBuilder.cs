using GameInfo.Protocol;

namespace GameInfoWebServer.Services;

internal static class InspectorRequestBuilder
{
    public static byte[] Build(int instanceId, uint requestId)
    {
        var scratch = new byte[ProtocolConstants.HeaderSize + 64];
        var payloadLen = InspectorRequestPayload.Write(scratch.AsSpan(ProtocolConstants.HeaderSize), instanceId);
        ProtocolHeader.Write(scratch.AsSpan(), MessageKind.InspectorRequest, requestId, payloadLen);
        var requestLen = ProtocolConstants.HeaderSize + payloadLen;
        var buffer = new byte[requestLen];
        scratch.AsSpan(0, requestLen).CopyTo(buffer);
        return buffer;
    }
}
