using GameInfo.Protocol;

namespace GameInfoWebServer.Services;

internal static class SnapshotFrameBuilder
{
    public static byte[]? BuildLatestFrame(GameSession session)
    {
        if (!session.TryGetLatestFrame(out var w, out var h, out var idx, out var jpeg) || jpeg is null)
        {
            return null;
        }

        var payloadLen = 4 + 4 + 8 + jpeg.Length;
        var total = ProtocolConstants.HeaderSize + payloadLen;
        var frame = new byte[total];
        ProtocolHeader.Write(frame.AsSpan(), MessageKind.FrameJpeg, 0, payloadLen);
        FrameJpegPayload.Write(frame.AsSpan(ProtocolConstants.HeaderSize), w, h, idx, jpeg);
        return frame;
    }

    public static byte[]? BuildHierarchyFrame(byte[]? hierarchyPayload)
    {
        if (hierarchyPayload is null)
        {
            return null;
        }

        var total = ProtocolConstants.HeaderSize + hierarchyPayload.Length;
        var frame = new byte[total];
        ProtocolHeader.Write(frame.AsSpan(), MessageKind.HierarchySnapshot, 0, hierarchyPayload.Length);
        hierarchyPayload.AsSpan().CopyTo(frame.AsSpan(ProtocolConstants.HeaderSize));
        return frame;
    }
}
