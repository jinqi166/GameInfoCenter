using System.Buffers;

namespace GameInfo.Protocol;

public sealed class PooledBuffer : IDisposable
{
    private byte[]? _array;

    public int Capacity => _array?.Length ?? 0;

    public Span<byte> WritableSpan
    {
        get
        {
            if (_array is null)
            {
                throw new ObjectDisposedException(nameof(PooledBuffer));
            }

            return _array.AsSpan();
        }
    }

    public static PooledBuffer Rent(int minimumLength)
    {
        var array = ArrayPool<byte>.Shared.Rent(minimumLength);
        return new PooledBuffer
        {
            _array = array,
        };
    }

    public ReadOnlySpan<byte> AsReadOnlySpan(int length)
    {
        if (_array is null)
        {
            throw new ObjectDisposedException(nameof(PooledBuffer));
        }

        if (length < 0 || length > _array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return _array.AsSpan(0, length);
    }

    public void Dispose()
    {
        if (_array is null)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(_array);
        _array = null;
    }
}
