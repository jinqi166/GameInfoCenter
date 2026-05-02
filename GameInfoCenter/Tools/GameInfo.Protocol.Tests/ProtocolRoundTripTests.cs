using System.Text;
using Xunit;

namespace GameInfo.Protocol.Tests;

public class ProtocolRoundTripTests
{
    [Fact]
    public void Header_round_trip()
    {
        Span<byte> buf = stackalloc byte[ProtocolConstants.HeaderSize];
        ProtocolHeader.Write(buf, MessageKind.FrameJpeg, 42, 100);
        Assert.True(ProtocolHeader.TryRead(buf, out var h, out var consumed));
        Assert.Equal(MessageKind.FrameJpeg, h.Kind);
        Assert.Equal(42u, h.RequestId);
        Assert.Equal(100, h.PayloadLength);
        Assert.Equal(ProtocolConstants.HeaderSize, consumed);
    }

    [Fact]
    public void SessionHello_round_trip()
    {
        var name = Encoding.UTF8.GetBytes("UnityEditor");
        var payloadLen = 4 + name.Length;
        var buf = new byte[payloadLen];
        var written = SessionHelloPayload.Write(buf, name);
        Assert.Equal(payloadLen, written);
        Assert.True(SessionHelloPayload.TryRead(buf, out var client, out var consumed));
        Assert.Equal("UnityEditor", client);
        Assert.Equal(payloadLen, consumed);
    }

    [Fact]
    public void FrameJpeg_round_trip()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var len = 16 + jpeg.Length;
        var buf = new byte[len];
        FrameJpegPayload.Write(buf, 1920, 1080, 7, jpeg);
        Assert.True(FrameJpegPayload.TryRead(buf, out var w, out var h, out var idx, out var j, out _));
        Assert.Equal(1920, w);
        Assert.Equal(1080, h);
        Assert.Equal(7ul, idx);
        Assert.Equal(jpeg, j.ToArray());
    }
}
