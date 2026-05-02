using System.Buffers.Binary;
using System.Text;

namespace GameInfo.Protocol;

public static class SessionHelloPayload
{
    public const int MaxClientNameUtf8Bytes = 256;

    public static int Write(Span<byte> destination, ReadOnlySpan<byte> clientNameUtf8)
    {
        if (clientNameUtf8.Length > MaxClientNameUtf8Bytes)
        {
            throw new ArgumentException("clientNameUtf8 too long.", nameof(clientNameUtf8));
        }

        if (destination.Length < 4 + clientNameUtf8.Length)
        {
            throw new ArgumentException("Destination too small.", nameof(destination));
        }

        BinaryPrimitives.WriteInt32LittleEndian(destination, clientNameUtf8.Length);
        clientNameUtf8.CopyTo(destination.Slice(4));
        return 4 + clientNameUtf8.Length;
    }

    public static bool TryRead(ReadOnlySpan<byte> source, out string clientName, out int bytesConsumed)
    {
        bytesConsumed = 0;
        clientName = string.Empty;

        if (source.Length < 4)
        {
            return false;
        }

        var nameLen = BinaryPrimitives.ReadInt32LittleEndian(source);
        if (nameLen < 0 || nameLen > MaxClientNameUtf8Bytes)
        {
            return false;
        }

        if (source.Length < 4 + nameLen)
        {
            return false;
        }

        clientName = Encoding.UTF8.GetString(source.Slice(4, nameLen));
        bytesConsumed = 4 + nameLen;
        return true;
    }
}
