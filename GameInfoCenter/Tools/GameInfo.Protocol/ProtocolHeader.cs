using System.Buffers.Binary;

namespace GameInfo.Protocol;

public readonly struct ProtocolHeader
{
    public ProtocolHeader(MessageKind kind, uint requestId, int payloadLength)
    {
        Kind = kind;
        RequestId = requestId;
        PayloadLength = payloadLength;
    }

    public MessageKind Kind { get; }

    public uint RequestId { get; }

    public int PayloadLength { get; }

    public static bool TryRead(ReadOnlySpan<byte> source, out ProtocolHeader header, out int bytesConsumed)
    {
        bytesConsumed = 0;
        header = default;

        if (source.Length < ProtocolConstants.HeaderSize)
        {
            return false;
        }

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(source);
        if (magic != ProtocolConstants.Magic)
        {
            return false;
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(4));
        if (version != ProtocolConstants.ProtocolVersion)
        {
            return false;
        }

        var kind = (MessageKind)BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(6));
        var requestId = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(8));
        var payloadLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(12)));
        if (payloadLength < 0)
        {
            return false;
        }

        header = new ProtocolHeader(kind, requestId, payloadLength);
        bytesConsumed = ProtocolConstants.HeaderSize;
        return true;
    }

    public static int Write(Span<byte> destination, MessageKind kind, uint requestId, int payloadLength)
    {
        if (destination.Length < ProtocolConstants.HeaderSize)
        {
            throw new ArgumentException("Destination too small for header.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination, ProtocolConstants.Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4), ProtocolConstants.ProtocolVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(6), (ushort)kind);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8), requestId);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(12), (uint)payloadLength);
        return ProtocolConstants.HeaderSize;
    }
}
