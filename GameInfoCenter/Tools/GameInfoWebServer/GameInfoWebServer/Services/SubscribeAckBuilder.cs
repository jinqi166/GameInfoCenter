using System.Text;
using GameInfo.Protocol;

namespace GameInfoWebServer.Services;

internal static class SubscribeAckBuilder
{
    public static byte[] Build(bool ok, string message)
    {
        var msgUtf8 = Encoding.UTF8.GetByteCount(message);
        var payloadLen = 1 + 4 + msgUtf8;
        var buf = new byte[ProtocolConstants.HeaderSize + payloadLen];
        ProtocolHeader.Write(buf.AsSpan(), MessageKind.ViewerSubscribeAck, 0, payloadLen);
        ViewerSubscribeAckPayload.Write(buf.AsSpan(ProtocolConstants.HeaderSize), ok, message);
        return buf;
    }
}
