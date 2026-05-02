using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using GameInfo.Protocol;
using UnityEngine;

namespace GameInfo.Runtime
{
    internal static class InspectorRuntimeSerializer
    {
        private const int MaxComponents = 32;

        private const int MaxPropertiesPerComponent = 48;

        public static bool TryBuildResponse(int instanceId, IReadOnlyDictionary<int, GameObject> instanceMap, uint requestId, out byte[] frame)
        {
            frame = Array.Empty<byte>();
            if (!instanceMap.TryGetValue(instanceId, out var go) || go == null)
            {
                return false;
            }

            var strings = new List<string>(128);
            var stringToId = new Dictionary<string, int>(128, StringComparer.Ordinal);

            int GetStringId(string? s)
            {
                if (s == null)
                {
                    s = string.Empty;
                }

                if (stringToId.TryGetValue(s, out var id))
                {
                    return id;
                }

                id = strings.Count;
                strings.Add(s);
                stringToId[s] = id;
                return id;
            }

            var props = new List<InspectorResponsePayload.PropertyRecord>(MaxComponents * MaxPropertiesPerComponent);

            void AddProp(string name, string typeName, string valueText)
            {
                var nameId = GetStringId(name);
                var typeId = GetStringId(typeName);
                var valueBytes = Encoding.UTF8.GetBytes(valueText ?? string.Empty);
                props.Add(new InspectorResponsePayload.PropertyRecord(nameId, typeId, PropertyValueKind.String, valueBytes));
            }

            AddProp("GameObject", typeof(GameObject).FullName ?? "GameObject", go.name);
            AddProp("activeSelf", typeof(bool).FullName ?? "bool", go.activeSelf ? "true" : "false");
            AddProp("layer", typeof(int).FullName ?? "int", go.layer.ToString());
            AddProp("tag", typeof(string).FullName ?? "string", go.tag);

            var components = go.GetComponents<Component>();
            var count = Mathf.Min(components.Length, MaxComponents);
            for (var c = 0; c < count; c++)
            {
                var comp = components[c];
                if (comp == null)
                {
                    continue;
                }

                var ct = comp.GetType();
                AddProp("component", ct.FullName ?? ct.Name, ct.Name);

                var fields = ct.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                var added = 0;
                for (var i = 0; i < fields.Length && added < MaxPropertiesPerComponent; i++)
                {
                    var f = fields[i];
                    if (f.IsLiteral)
                    {
                        continue;
                    }

                    object? val;
                    try
                    {
                        val = f.GetValue(comp);
                    }
                    catch
                    {
                        continue;
                    }

                    var text = FormatValue(val);
                    AddProp(f.Name, f.FieldType.Name, text);
                    added++;
                }
            }

            var estimate = 65536;
            var buffer = new byte[estimate];
            var written = 0;
            while (true)
            {
                try
                {
                    written = InspectorResponsePayload.Write(buffer.AsSpan(), instanceId, strings, props);
                    break;
                }
                catch
                {
                    estimate *= 2;
                    buffer = new byte[estimate];
                }
            }

            var payload = new byte[written];
            buffer.AsSpan(0, written).CopyTo(payload);
            var total = ProtocolConstants.HeaderSize + written;
            frame = new byte[total];
            ProtocolHeader.Write(frame.AsSpan(), MessageKind.InspectorResponse, requestId, written);
            payload.AsSpan().CopyTo(frame.AsSpan(ProtocolConstants.HeaderSize));
            return true;
        }

        private static string FormatValue(object? val)
        {
            if (val == null)
            {
                return "null";
            }

            if (val is Vector3 v3)
            {
                return v3.ToString();
            }

            if (val is Vector2 v2)
            {
                return v2.ToString();
            }

            if (val is Quaternion q)
            {
                return q.ToString();
            }

            return val.ToString() ?? string.Empty;
        }
    }
}
