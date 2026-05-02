namespace GameInfo.Protocol;

public enum MessageKind : ushort
{
    SessionHello = 1,
    Heartbeat = 2,
    FrameJpeg = 3,
    HierarchySnapshot = 4,
    InspectorRequest = 5,
    InspectorResponse = 6,
    ViewerSubscribe = 7,
    ViewerSubscribeAck = 8,
    Error = 9,
}
