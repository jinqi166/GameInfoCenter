using System.Buffers.Binary;
using System.Text;

namespace GameInfo.Protocol;

public static class HierarchySnapshotPayload
{
    public static int WriteStringTable(Span<byte> destination, IReadOnlyList<string> strings)
    {
        if (destination.Length < 4)
        {
            throw new ArgumentException("Destination too small.", nameof(destination));
        }

        BinaryPrimitives.WriteInt32LittleEndian(destination, strings.Count);
        var offset = 4;
        for (var i = 0; i < strings.Count; i++)
        {
            var utf8 = Encoding.UTF8.GetBytes(strings[i]);
            var need = 4 + utf8.Length;
            if (destination.Length - offset < need)
            {
                throw new ArgumentException("Destination too small for string table.", nameof(destination));
            }

            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), utf8.Length);
            offset += 4;
            utf8.CopyTo(destination.Slice(offset));
            offset += utf8.Length;
        }

        return offset;
    }

    public static bool TryReadStringTable(ReadOnlySpan<byte> source, out string[] strings, out int bytesConsumed)
    {
        strings = Array.Empty<string>();
        bytesConsumed = 0;

        if (source.Length < 4)
        {
            return false;
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(source);
        if (count < 0 || count > 1_000_000)
        {
            return false;
        }

        var list = new string[count];
        var offset = 4;
        for (var i = 0; i < count; i++)
        {
            if (source.Length - offset < 4)
            {
                return false;
            }

            var byteLen = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset));
            offset += 4;
            if (byteLen < 0 || source.Length - offset < byteLen)
            {
                return false;
            }

            list[i] = Encoding.UTF8.GetString(source.Slice(offset, byteLen));
            offset += byteLen;
        }

        strings = list;
        bytesConsumed = offset;
        return true;
    }

    public readonly struct NodeRecord
    {
        public NodeRecord(int instanceId, int parentInstanceId, int nameId, int depth, int siblingIndex, bool activeSelf, int layer, int tagNameId)
        {
            InstanceId = instanceId;
            ParentInstanceId = parentInstanceId;
            NameId = nameId;
            Depth = depth;
            SiblingIndex = siblingIndex;
            ActiveSelf = activeSelf;
            Layer = layer;
            TagNameId = tagNameId;
        }

        public int InstanceId { get; }

        public int ParentInstanceId { get; }

        public int NameId { get; }

        public int Depth { get; }

        public int SiblingIndex { get; }

        public bool ActiveSelf { get; }

        public int Layer { get; }

        public int TagNameId { get; }

        public const int SerializedSize = 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4;

        public int Write(Span<byte> destination)
        {
            if (destination.Length < SerializedSize)
            {
                throw new ArgumentException("Destination too small.", nameof(destination));
            }

            BinaryPrimitives.WriteInt32LittleEndian(destination, InstanceId);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(4), ParentInstanceId);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(8), NameId);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(12), Depth);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(16), SiblingIndex);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(20), ActiveSelf ? 1 : 0);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(24), Layer);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(28), TagNameId);
            return SerializedSize;
        }

        public static bool TryRead(ReadOnlySpan<byte> source, out NodeRecord node, out int bytesConsumed)
        {
            bytesConsumed = 0;
            node = default;

            if (source.Length < SerializedSize)
            {
                return false;
            }

            var instanceId = BinaryPrimitives.ReadInt32LittleEndian(source);
            var parentInstanceId = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(4));
            var nameId = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(8));
            var depth = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(12));
            var siblingIndex = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(16));
            var active = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(20)) != 0;
            var layer = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(24));
            var tagNameId = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(28));
            node = new NodeRecord(instanceId, parentInstanceId, nameId, depth, siblingIndex, active, layer, tagNameId);
            bytesConsumed = SerializedSize;
            return true;
        }
    }

    public static int WriteNodes(Span<byte> destination, IReadOnlyList<NodeRecord> nodes)
    {
        if (destination.Length < 4)
        {
            throw new ArgumentException("Destination too small.", nameof(destination));
        }

        BinaryPrimitives.WriteInt32LittleEndian(destination, nodes.Count);
        var offset = 4;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (destination.Length - offset < NodeRecord.SerializedSize)
            {
                throw new ArgumentException("Destination too small for nodes.", nameof(destination));
            }

            offset += nodes[i].Write(destination.Slice(offset));
        }

        return offset;
    }

    public static bool TryReadNodes(ReadOnlySpan<byte> source, out NodeRecord[] nodes, out int bytesConsumed)
    {
        nodes = Array.Empty<NodeRecord>();
        bytesConsumed = 0;

        if (source.Length < 4)
        {
            return false;
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(source);
        if (count < 0 || count > 1_000_000)
        {
            return false;
        }

        var list = new NodeRecord[count];
        var offset = 4;
        for (var i = 0; i < count; i++)
        {
            if (!NodeRecord.TryRead(source.Slice(offset), out var node, out var consumed))
            {
                return false;
            }

            list[i] = node;
            offset += consumed;
        }

        nodes = list;
        bytesConsumed = offset;
        return true;
    }
}
