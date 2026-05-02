using System.Buffers.Binary;

namespace GameInfo.Protocol;

public static class FrameJpegPayload
{
    public static int Write(Span<byte> destination, int width, int height, ulong frameIndex, ReadOnlySpan<byte> jpegBytes)
    {
        const int prefix = 4 + 4 + 8;
        if (destination.Length < prefix + jpegBytes.Length)
        {
            throw new ArgumentException("Destination too small.", nameof(destination));
        }

        BinaryPrimitives.WriteInt32LittleEndian(destination, width);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(4), height);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(8), frameIndex);
        jpegBytes.CopyTo(destination.Slice(prefix));
        return prefix + jpegBytes.Length;
    }

    public static bool TryRead(ReadOnlySpan<byte> source, out int width, out int height, out ulong frameIndex, out ReadOnlySpan<byte> jpegBytes, out int bytesConsumed)
    {
        width = 0;
        height = 0;
        frameIndex = 0;
        jpegBytes = ReadOnlySpan<byte>.Empty;
        bytesConsumed = 0;

        const int prefix = 4 + 4 + 8;
        if (source.Length < prefix)
        {
            return false;
        }

        width = BinaryPrimitives.ReadInt32LittleEndian(source);
        height = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(4));
        frameIndex = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(8));
        jpegBytes = source.Slice(prefix);
        bytesConsumed = source.Length;
        return width > 0 && height > 0;
    }
}
