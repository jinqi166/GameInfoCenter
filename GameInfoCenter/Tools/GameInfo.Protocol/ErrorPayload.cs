using System.Buffers.Binary;
using System.Text;

namespace GameInfo.Protocol;

public static class ErrorPayload
{
    public static int Write(Span<byte> destination, int code, ReadOnlySpan<char> message)
    {
        var msgUtf8Len = Encoding.UTF8.GetByteCount(message);
        if (destination.Length < 4 + 4 + msgUtf8Len)
        {
            throw new ArgumentException("Destination too small.", nameof(destination));
        }

        BinaryPrimitives.WriteInt32LittleEndian(destination, code);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(4), msgUtf8Len);
        Encoding.UTF8.GetBytes(message, destination.Slice(8));
        return 4 + 4 + msgUtf8Len;
    }

    public static bool TryRead(ReadOnlySpan<byte> source, out int code, out string message, out int bytesConsumed)
    {
        code = 0;
        message = string.Empty;
        bytesConsumed = 0;

        if (source.Length < 8)
        {
            return false;
        }

        code = BinaryPrimitives.ReadInt32LittleEndian(source);
        var msgLen = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(4));
        if (msgLen < 0 || source.Length < 8 + msgLen)
        {
            return false;
        }

        message = Encoding.UTF8.GetString(source.Slice(8, msgLen));
        bytesConsumed = 8 + msgLen;
        return true;
    }
}
