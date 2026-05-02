using System.Buffers.Binary;
using System.Text;

namespace GameInfo.Protocol;

public static class ViewerSubscribePayload
{
    public static int Write(Span<byte> destination, ReadOnlySpan<char> sessionId, ReadOnlySpan<char> viewerToken)
    {
        var sessionUtf8Length = Encoding.UTF8.GetByteCount(sessionId);
        var tokenUtf8Length = Encoding.UTF8.GetByteCount(viewerToken);
        var need = 4 + sessionUtf8Length + 4 + tokenUtf8Length;
        if (destination.Length < need)
        {
            throw new ArgumentException("Destination too small.", nameof(destination));
        }

        BinaryPrimitives.WriteInt32LittleEndian(destination, sessionUtf8Length);
        Encoding.UTF8.GetBytes(sessionId, destination.Slice(4));
        var offset = 4 + sessionUtf8Length;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), tokenUtf8Length);
        offset += 4;
        Encoding.UTF8.GetBytes(viewerToken, destination.Slice(offset));
        offset += tokenUtf8Length;
        return offset;
    }

    public static bool TryRead(ReadOnlySpan<byte> source, out string sessionId, out string viewerToken, out int bytesConsumed)
    {
        sessionId = string.Empty;
        viewerToken = string.Empty;
        bytesConsumed = 0;

        if (source.Length < 4)
        {
            return false;
        }

        var sLen = BinaryPrimitives.ReadInt32LittleEndian(source);
        if (sLen < 0 || sLen > 256 || source.Length < 4 + sLen + 4)
        {
            return false;
        }

        sessionId = Encoding.UTF8.GetString(source.Slice(4, sLen));
        var offset = 4 + sLen;
        var tLen = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset));
        offset += 4;
        if (tLen < 0 || tLen > 512 || source.Length < offset + tLen)
        {
            return false;
        }

        viewerToken = Encoding.UTF8.GetString(source.Slice(offset, tLen));
        offset += tLen;
        bytesConsumed = offset;
        return true;
    }
}
