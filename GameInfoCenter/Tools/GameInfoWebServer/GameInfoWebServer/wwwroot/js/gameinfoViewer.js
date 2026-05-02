(function () {
    const MAGIC = 0x464e4947;
    const VERSION = 1;
    const HEADER = 16;
    const MK = {
        SessionHello: 1,
        Heartbeat: 2,
        FrameJpeg: 3,
        HierarchySnapshot: 4,
        InspectorRequest: 5,
        InspectorResponse: 6,
        ViewerSubscribe: 7,
        ViewerSubscribeAck: 8,
        Error: 9
    };

    function readHeader(u8, offset) {
        const dv = new DataView(u8.buffer, u8.byteOffset + offset, HEADER);
        const magic = dv.getUint32(0, true);
        if (magic !== MAGIC) return null;
        const ver = dv.getUint16(4, true);
        if (ver !== VERSION) return null;
        const kind = dv.getUint16(6, true);
        const requestId = dv.getUint32(8, true);
        const payloadLen = dv.getUint32(12, true);
        return { kind, requestId, payloadLen, total: HEADER + payloadLen };
    }

    function buildViewerSubscribe(sessionId, viewerToken) {
        const enc = new TextEncoder();
        const s = enc.encode(sessionId);
        const t = enc.encode(viewerToken);
        const payloadLen = 4 + s.length + 4 + t.length;
        const buf = new ArrayBuffer(HEADER + payloadLen);
        const u8 = new Uint8Array(buf);
        const dv = new DataView(buf);
        dv.setUint32(0, MAGIC, true);
        dv.setUint16(4, VERSION, true);
        dv.setUint16(6, MK.ViewerSubscribe, true);
        dv.setUint32(8, 0, true);
        dv.setUint32(12, payloadLen, true);
        let o = HEADER;
        dv.setInt32(o, s.length, true);
        o += 4;
        u8.set(s, o);
        o += s.length;
        dv.setInt32(o, t.length, true);
        o += 4;
        u8.set(t, o);
        return buf;
    }

    function buildInspectorRequest(instanceId, requestId) {
        const buf = new ArrayBuffer(HEADER + 4);
        const u8 = new Uint8Array(buf);
        const dv = new DataView(buf);
        dv.setUint32(0, MAGIC, true);
        dv.setUint16(4, VERSION, true);
        dv.setUint16(6, MK.InspectorRequest, true);
        dv.setUint32(8, requestId >>> 0, true);
        dv.setUint32(12, 4, true);
        dv.setInt32(HEADER, instanceId, true);
        return buf;
    }

    function parseStringTable(u8, offset) {
        const dv = new DataView(u8.buffer, u8.byteOffset, u8.byteLength);
        let o = offset;
        const count = dv.getInt32(o, true);
        o += 4;
        const strings = [];
        for (let i = 0; i < count; i++) {
            const len = dv.getInt32(o, true);
            o += 4;
            const slice = u8.subarray(o, o + len);
            strings.push(new TextDecoder().decode(slice));
            o += len;
        }
        return { strings, nextOffset: o };
    }

    function parseHierarchyPayload(payload) {
        const u8 = new Uint8Array(payload);
        const st = parseStringTable(u8, 0);
        let o = st.nextOffset;
        const dv = new DataView(u8.buffer, u8.byteOffset, u8.byteLength);
        const nodeCount = dv.getInt32(o, true);
        o += 4;
        const nodes = [];
        for (let i = 0; i < nodeCount; i++) {
            const instanceId = dv.getInt32(o, true);
            const parentInstanceId = dv.getInt32(o + 4, true);
            const nameId = dv.getInt32(o + 8, true);
            const depth = dv.getInt32(o + 12, true);
            const siblingIndex = dv.getInt32(o + 16, true);
            const activeSelf = dv.getInt32(o + 20, true) !== 0;
            const layer = dv.getInt32(o + 24, true);
            const tagNameId = dv.getInt32(o + 28, true);
            nodes.push({
                instanceId,
                parentInstanceId,
                nameId,
                depth,
                siblingIndex,
                activeSelf,
                layer,
                tagNameId
            });
            o += 32;
        }
        return { strings: st.strings, nodes };
    }

    function parseInspectorResponse(payload) {
        const u8 = new Uint8Array(payload);
        const st = parseStringTable(u8, 0);
        let o = st.nextOffset;
        const dv = new DataView(u8.buffer, u8.byteOffset, u8.byteLength);
        const instanceId = dv.getInt32(o, true);
        const propCount = dv.getInt32(o + 4, true);
        o += 8;
        const props = [];
        for (let i = 0; i < propCount; i++) {
            const nameId = dv.getInt32(o, true);
            const typeNameId = dv.getInt32(o + 4, true);
            const kind = u8[o + 8];
            const valueLen = dv.getInt32(o + 9, true);
            const valueBytes = u8.subarray(o + 13, o + 13 + valueLen);
            props.push({ nameId, typeNameId, kind, valueBytes });
            o += 13 + valueLen;
        }
        return { strings: st.strings, instanceId, props };
    }

    function renderHierarchy(container, data, onSelect) {
        container.innerHTML = "";
        const ul = document.createElement("ul");
        ul.className = "list-unstyled hierarchy-tree";
        const byParent = new Map();
        for (const n of data.nodes) {
            const key = n.parentInstanceId;
            if (!byParent.has(key)) byParent.set(key, []);
            byParent.get(key).push(n);
        }
        const roots = byParent.get(0) || [];
        function appendChildren(parentUl, parentId, depth) {
            const children = byParent.get(parentId) || [];
            children.sort((a, b) => a.siblingIndex - b.siblingIndex);
            for (const n of children) {
                const li = document.createElement("li");
                li.style.paddingLeft = depth * 12 + "px";
                const name = data.strings[n.nameId] || ("#" + n.nameId);
                const label = document.createElement("span");
                label.textContent = name + (n.activeSelf ? "" : " (inactive)");
                label.style.cursor = "pointer";
                label.addEventListener("click", () => onSelect(n.instanceId));
                li.appendChild(label);
                parentUl.appendChild(li);
                const sub = document.createElement("ul");
                sub.className = "list-unstyled";
                parentUl.appendChild(sub);
                appendChildren(sub, n.instanceId, depth + 1);
            }
        }
        appendChildren(ul, 0, 0);
        container.appendChild(ul);
    }

    function renderInspector(container, data) {
        container.innerHTML = "";
        const table = document.createElement("table");
        table.className = "table table-sm";
        const thead = document.createElement("thead");
        thead.innerHTML = "<tr><th>Name</th><th>Type</th><th>Value</th></tr>";
        table.appendChild(thead);
        const tbody = document.createElement("tbody");
        for (const p of data.props) {
            const tr = document.createElement("tr");
            const name = data.strings[p.nameId] || ("#" + p.nameId);
            const typeName = data.strings[p.typeNameId] || ("#" + p.typeNameId);
            const valText = new TextDecoder().decode(p.valueBytes);
            tr.innerHTML = "<td></td><td></td><td></td>";
            tr.cells[0].textContent = name;
            tr.cells[1].textContent = typeName;
            tr.cells[2].textContent = valText;
            tbody.appendChild(tr);
        }
        table.appendChild(tbody);
        container.appendChild(table);
    }

    let bitmap = null;

    async function drawJpegToCanvas(canvas, jpegBytes) {
        const blob = new Blob([jpegBytes], { type: "image/jpeg" });
        if (bitmap) {
            bitmap.close();
            bitmap = null;
        }
        bitmap = await createImageBitmap(blob);
        canvas.width = bitmap.width;
        canvas.height = bitmap.height;
        const ctx = canvas.getContext("2d");
        ctx.drawImage(bitmap, 0, 0);
    }

    function start(canvasId, hierarchyId, inspectorId, sessionId, viewerToken) {
            const canvas = document.getElementById(canvasId);
            const hierarchyEl = document.getElementById(hierarchyId);
            const inspectorEl = document.getElementById(inspectorId);
            if (!canvas || !hierarchyEl || !inspectorEl) return;

            const scheme = location.protocol === "https:" ? "wss:" : "ws:";
            const wsUrl = scheme + "//" + location.host + "/ws/view";
            const ws = new WebSocket(wsUrl);
            ws.binaryType = "arraybuffer";

            let requestSeq = 1;

            ws.onopen = function () {
                const sub = buildViewerSubscribe(sessionId, viewerToken);
                ws.send(sub);
            };

            ws.onmessage = async function (ev) {
                const buf = ev.data;
                if (!(buf instanceof ArrayBuffer)) return;
                const u8 = new Uint8Array(buf);
                const hdr = readHeader(u8, 0);
                if (!hdr) return;
                const payload = u8.subarray(HEADER, HEADER + hdr.payloadLen);
                if (hdr.kind === MK.ViewerSubscribeAck) {
                    return;
                }
                if (hdr.kind === MK.FrameJpeg) {
                    const dv = new DataView(buf);
                    const w = dv.getInt32(HEADER, true);
                    const h = dv.getInt32(HEADER + 4, true);
                    const jpeg = u8.subarray(HEADER + 16);
                    await drawJpegToCanvas(canvas, jpeg);
                    return;
                }
                if (hdr.kind === MK.HierarchySnapshot) {
                    try {
                        const data = parseHierarchyPayload(payload);
                        renderHierarchy(hierarchyEl, data, function (instanceId) {
                            const rid = (requestSeq++) >>> 0;
                            const req = buildInspectorRequest(instanceId, rid);
                            ws.send(req);
                        });
                    } catch (e) {
                        console.warn("hierarchy parse", e);
                    }
                    return;
                }
                if (hdr.kind === MK.InspectorResponse) {
                    try {
                        const data = parseInspectorResponse(payload);
                        renderInspector(inspectorEl, data);
                    } catch (e) {
                        console.warn("inspector parse", e);
                    }
                }
            };

            ws.onerror = function () {
                console.warn("gameinfo ws error");
            };
    }

    window.gameinfoViewer = { start: start };
    window.startGameInfoViewer = start;
})();
