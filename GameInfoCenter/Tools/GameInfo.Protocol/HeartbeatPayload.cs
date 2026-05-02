using System.Buffers.Binary;

namespace GameInfo.Protocol;

public static class HeartbeatPayload
{
    public static int Write(Span<byte> destination, ulong sequence, long unixMs)
    {
        if (destination.Length < 16)
        {
            throw new ArgumentException("Destination too small.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt64LittleEndian(destination, sequence);
        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(8), unixMs);
        return 16;
    }

    public static bool TryRead(ReadOnlySpan<byte> source, out ulong sequence, out long unixMs, out int bytesConsumed)
    {
        sequence = 0;
        unixMs = 0;
        bytesConsumed = 0;

        if (source.Length < 16)
        {
            return false;
        }

        sequence = BinaryPrimitives.ReadUInt64LittleEndian(source);
        unixMs = BinaryPrimitives.ReadInt64LittleEndian(source.Slice(8));
        bytesConsumed = 16;
        return true;
    }
}
