namespace GameInfo.Protocol;

public static class ProtocolFrame
{
    public static int GetTotalFrameSize(int payloadLength)
    {
        return ProtocolConstants.HeaderSize + payloadLength;
    }

    public static int WriteFrame(Span<byte> destination, MessageKind kind, uint requestId, ReadOnlySpan<byte> payload)
    {
        var total = ProtocolConstants.HeaderSize + payload.Length;
        if (destination.Length < total)
        {
            throw new ArgumentException("Destination too small for frame.", nameof(destination));
        }

        ProtocolHeader.Write(destination, kind, requestId, payload.Length);
        payload.CopyTo(destination.Slice(ProtocolConstants.HeaderSize));
        return total;
    }
}
