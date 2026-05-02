using System.Net.WebSockets;
using GameInfo.Protocol;

namespace GameInfoWebServer.Services;

internal static class WebSocketFrameReader
{
    public static async Task<byte[]?> ReadBinaryFrameAsync(WebSocket socket, byte[] scratch, CancellationToken cancellationToken)
    {
        var headerReceived = 0;
        while (headerReceived < ProtocolConstants.HeaderSize)
        {
            var r = await socket.ReceiveAsync(scratch.AsMemory(headerReceived), cancellationToken).ConfigureAwait(false);
            if (r.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (r.MessageType != WebSocketMessageType.Binary)
            {
                return null;
            }

            headerReceived += r.Count;
            if (r.EndOfMessage && headerReceived < ProtocolConstants.HeaderSize)
            {
                return null;
            }
        }

        if (!ProtocolHeader.TryRead(scratch.AsSpan(0, ProtocolConstants.HeaderSize), out var header, out _))
        {
            return null;
        }

        var total = ProtocolConstants.HeaderSize + header.PayloadLength;
        if (total < 0 || total > scratch.Length)
        {
            return null;
        }

        var frame = new byte[total];
        scratch.AsSpan(0, ProtocolConstants.HeaderSize).CopyTo(frame);

        var offset = ProtocolConstants.HeaderSize;
        while (offset < total)
        {
            var r = await socket.ReceiveAsync(frame.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (r.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (r.MessageType != WebSocketMessageType.Binary)
            {
                return null;
            }

            offset += r.Count;
            if (r.EndOfMessage && offset < total)
            {
                return null;
            }
        }

        return frame;
    }
}
