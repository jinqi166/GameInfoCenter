using System.Buffers.Binary;
using System.Text;

namespace GameInfo.Protocol;

public static class ViewerSubscribeAckPayload
{
    public static int Write(Span<byte> destination, bool ok, ReadOnlySpan<char> message)
    {
        var msgUtf8Len = Encoding.UTF8.GetByteCount(message);
        if (destination.Length < 1 + 4 + msgUtf8Len)
        {
            throw new ArgumentException("Destination too small.", nameof(destination));
        }

        destination[0] = ok ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1), msgUtf8Len);
        Encoding.UTF8.GetBytes(message, destination.Slice(5));
        return 1 + 4 + msgUtf8Len;
    }

    public static bool TryRead(ReadOnlySpan<byte> source, out bool ok, out string message, out int bytesConsumed)
    {
        ok = false;
        message = string.Empty;
        bytesConsumed = 0;

        if (source.Length < 1 + 4)
        {
            return false;
        }

        ok = source[0] != 0;
        var msgLen = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(1));
        if (msgLen < 0 || source.Length < 5 + msgLen)
        {
            return false;
        }

        message = Encoding.UTF8.GetString(source.Slice(5, msgLen));
        bytesConsumed = 5 + msgLen;
        return true;
    }
}
