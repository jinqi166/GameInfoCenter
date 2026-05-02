using System;
using System.Collections.Generic;
using GameInfo.Protocol;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameInfo.Runtime
{
    internal static class HierarchySnapshotSerializer
    {
        public static bool TryBuild(Dictionary<int, GameObject> instanceMap, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            instanceMap.Clear();
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return false;
            }

            var roots = scene.GetRootGameObjects();
            var stringToId = new Dictionary<string, int>(256, StringComparer.Ordinal);
            var strings = new List<string>(256);
            var nodes = new List<HierarchySnapshotPayload.NodeRecord>(1024);

            int GetStringId(string? s)
            {
                if (string.IsNullOrEmpty(s))
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

            void Visit(GameObject go, int parentInstanceId, int depth)
            {
                instanceMap[go.GetInstanceID()] = go;
                var t = go.transform;
                var nameId = GetStringId(go.name);
                var tagId = GetStringId(go.tag);
                nodes.Add(new HierarchySnapshotPayload.NodeRecord(
                    go.GetInstanceID(),
                    parentInstanceId,
                    nameId,
                    depth,
                    t.GetSiblingIndex(),
                    go.activeSelf,
                    go.layer,
                    tagId));

                for (var i = 0; i < t.childCount; i++)
                {
                    var child = t.GetChild(i).gameObject;
                    Visit(child, go.GetInstanceID(), depth + 1);
                }
            }

            for (var i = 0; i < roots.Length; i++)
            {
                Visit(roots[i], 0, 0);
            }

            var estimate = 65536;
            var buffer = new byte[estimate];
            var written = 0;
            while (true)
            {
                try
                {
                    written = HierarchySnapshotPayload.WriteStringTable(buffer.AsSpan(), strings);
                    written += HierarchySnapshotPayload.WriteNodes(buffer.AsSpan(written), nodes);
                    break;
                }
                catch
                {
                    estimate *= 2;
                    buffer = new byte[estimate];
                }
            }

            payload = new byte[written];
            buffer.AsSpan(0, written).CopyTo(payload);
            return true;
        }
    }
}
