using System.Buffers.Binary;
using System.Text;

namespace GameInfo.Protocol;

public static class InspectorRequestPayload
{
    public static int Write(Span<byte> destination, int instanceId)
    {
        if (destination.Length < 4)
        {
            throw new ArgumentException("Destination too small.", nameof(destination));
        }

        BinaryPrimitives.WriteInt32LittleEndian(destination, instanceId);
        return 4;
    }

    public static bool TryRead(ReadOnlySpan<byte> source, out int instanceId, out int bytesConsumed)
    {
        bytesConsumed = 0;
        instanceId = 0;

        if (source.Length < 4)
        {
            return false;
        }

        instanceId = BinaryPrimitives.ReadInt32LittleEndian(source);
        bytesConsumed = 4;
        return true;
    }
}

public enum PropertyValueKind : byte
{
    Null = 0,
    Boolean = 1,
    Int32 = 2,
    Int64 = 3,
    Single = 4,
    Double = 5,
    String = 6,
    Enum = 7,
}

public static class InspectorResponsePayload
{
    public static int Write(Span<byte> destination, int instanceId, IReadOnlyList<string> stringTable, IReadOnlyList<PropertyRecord> properties)
    {
        var stringTableBytes = HierarchySnapshotPayload.WriteStringTable(destination, stringTable);
        var offset = stringTableBytes;
        if (destination.Length - offset < 8)
        {
            throw new ArgumentException("Destination too small.", nameof(destination));
        }

        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), instanceId);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), properties.Count);
        offset += 4;

        for (var i = 0; i < properties.Count; i++)
        {
            offset += properties[i].Write(destination.Slice(offset));
        }

        return offset;
    }

    public static bool TryRead(ReadOnlySpan<byte> source, out int instanceId, out string[] stringTable, out PropertyRecord[] properties, out int bytesConsumed)
    {
        instanceId = 0;
        stringTable = Array.Empty<string>();
        properties = Array.Empty<PropertyRecord>();
        bytesConsumed = 0;

        if (!HierarchySnapshotPayload.TryReadStringTable(source, out stringTable, out var stConsumed))
        {
            return false;
        }

        var rest = source.Slice(stConsumed);
        if (rest.Length < 8)
        {
            return false;
        }

        instanceId = BinaryPrimitives.ReadInt32LittleEndian(rest);
        var propCount = BinaryPrimitives.ReadInt32LittleEndian(rest.Slice(4));
        if (propCount < 0 || propCount > 100_000)
        {
            return false;
        }

        var offset = stConsumed + 8;
        var list = new PropertyRecord[propCount];
        for (var i = 0; i < propCount; i++)
        {
            if (!PropertyRecord.TryRead(source.Slice(offset), out var prop, out var consumed))
            {
                return false;
            }

            list[i] = prop;
            offset += consumed;
        }

        properties = list;
        bytesConsumed = offset;
        return true;
    }

    public readonly struct PropertyRecord
    {
        public PropertyRecord(int nameId, int typeNameId, PropertyValueKind kind, byte[] valueBytes)
        {
            NameId = nameId;
            TypeNameId = typeNameId;
            Kind = kind;
            ValueBytes = valueBytes;
        }

        public int NameId { get; }

        public int TypeNameId { get; }

        public PropertyValueKind Kind { get; }

        public byte[] ValueBytes { get; }

        public int Write(Span<byte> destination)
        {
            var valueLen = ValueBytes.Length;
            var need = 4 + 4 + 1 + 4 + valueLen;
            if (destination.Length < need)
            {
                throw new ArgumentException("Destination too small.", nameof(destination));
            }

            BinaryPrimitives.WriteInt32LittleEndian(destination, NameId);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(4), TypeNameId);
            destination[8] = (byte)Kind;
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(9), valueLen);
            ValueBytes.AsSpan().CopyTo(destination.Slice(13));
            return need;
        }

        public static bool TryRead(ReadOnlySpan<byte> source, out PropertyRecord prop, out int bytesConsumed)
        {
            prop = default;
            bytesConsumed = 0;

            if (source.Length < 4 + 4 + 1 + 4)
            {
                return false;
            }

            var nameId = BinaryPrimitives.ReadInt32LittleEndian(source);
            var typeNameId = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(4));
            var kind = (PropertyValueKind)source[8];
            var valueLen = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(9));
            if (valueLen < 0 || source.Length < 13 + valueLen)
            {
                return false;
            }

            var valueBytes = new byte[valueLen];
            source.Slice(13, valueLen).CopyTo(valueBytes);
            prop = new PropertyRecord(nameId, typeNameId, kind, valueBytes);
            bytesConsumed = 13 + valueLen;
            return true;
        }
    }
}
