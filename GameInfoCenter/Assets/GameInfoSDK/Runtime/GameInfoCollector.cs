using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameInfo.Protocol;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace GameInfo.Runtime
{
    public sealed class GameInfoCollector : MonoBehaviour
    {
        [SerializeField]
        private string serverBaseUrl = "http://localhost:5000/";

        [SerializeField]
        private bool autoCreateSession = true;

        [SerializeField]
        private string manualSessionId = string.Empty;

        [SerializeField]
        private string manualIngestToken = string.Empty;

        [SerializeField]
        private Camera targetCamera;

        [SerializeField]
        private int captureMaxWidth = 1280;

        [SerializeField]
        [Range(1, 100)]
        private int jpegQuality = 60;

        [SerializeField]
        private float maxFrameRate = 15f;

        [SerializeField]
        private float hierarchySendIntervalSeconds = 1f;

        [SerializeField]
        private float heartbeatIntervalSeconds = 5f;

        private IngestWebSocketClient? _ingest;

        private string _sessionId = string.Empty;

        private string _ingestToken = string.Empty;

        private ulong _heartbeatSequence;

        private float _nextFrameUtc;

        private float _nextHierarchyUtc;

        private float _nextHeartbeatUtc;

        private RenderTexture? _rt;

        private Texture2D? _readTex;

        private readonly Dictionary<int, GameObject> _instanceMap = new Dictionary<int, GameObject>(4096);

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void Start()
        {
            if (autoCreateSession)
            {
                StartCoroutine(CreateSessionCoroutine());
            }
            else
            {
                _sessionId = manualSessionId;
                _ingestToken = manualIngestToken;
                StartIngestIfReady();
            }

            _nextHeartbeatUtc = Time.realtimeSinceStartup;
        }

        private void OnDestroy()
        {
            _ingest?.Dispose();
            _ingest = null;
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }

            if (_readTex != null)
            {
                Destroy(_readTex);
                _readTex = null;
            }
        }

        private void Update()
        {
            MainThreadDispatcher.ExecutePending();
            if (_ingest == null)
            {
                return;
            }

            SendHeartbeatIfDue();
            TrySendHierarchy();
            TryCaptureFrame();
        }

        private IEnumerator CreateSessionCoroutine()
        {
            var baseUrl = serverBaseUrl.TrimEnd('/');
            var url = baseUrl + "/api/sessions";
            var body = Encoding.UTF8.GetBytes("{}");
            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("GameInfo session create failed: " + req.error);
                yield break;
            }

            var dto = JsonUtility.FromJson<CreateSessionResponseDto>(req.downloadHandler.text);
            if (dto == null || string.IsNullOrEmpty(dto.sessionId) || string.IsNullOrEmpty(dto.ingestToken))
            {
                Debug.LogWarning("GameInfo session create invalid response");
                yield break;
            }

            _sessionId = dto.sessionId;
            _ingestToken = dto.ingestToken;
            StartIngestIfReady();
        }

        private void StartIngestIfReady()
        {
            if (string.IsNullOrEmpty(_sessionId) || string.IsNullOrEmpty(_ingestToken))
            {
                return;
            }

            var ws = BuildWebSocketIngestUri(serverBaseUrl, _sessionId, _ingestToken);
            _ingest = new IngestWebSocketClient(ws);
            _ingest.BinaryMessageReceived += OnIngestBinary;
            _ingest.Start();
        }

        private static string BuildWebSocketIngestUri(string baseUrl, string sessionId, string token)
        {
            var uri = new Uri(baseUrl);
            var scheme = uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            var authority = uri.Authority;
            var sid = Uri.EscapeDataString(sessionId);
            var tok = Uri.EscapeDataString(token);
            return $"{scheme}://{authority}/ws/ingest?sessionId={sid}&token={tok}";
        }

        private void SendHeartbeatIfDue()
        {
            if (string.IsNullOrEmpty(_sessionId))
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (now < _nextHeartbeatUtc)
            {
                return;
            }

            _nextHeartbeatUtc = now + heartbeatIntervalSeconds;
            var baseUrl = serverBaseUrl.TrimEnd('/');
            var url = $"{baseUrl}/api/sessions/{Uri.EscapeDataString(_sessionId)}/heartbeat?sequence={_heartbeatSequence}";
            _heartbeatSequence++;
            using var req = UnityWebRequest.Get(url);
            req.SendWebRequest();
        }

        private void TrySendHierarchy()
        {
            if (_ingest == null)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (now < _nextHierarchyUtc)
            {
                return;
            }

            _nextHierarchyUtc = now + hierarchySendIntervalSeconds;
            if (!HierarchySnapshotSerializer.TryBuild(_instanceMap, out var payload))
            {
                return;
            }

            var total = ProtocolConstants.HeaderSize + payload.Length;
            var rented = ArrayPool<byte>.Shared.Rent(total);
            ProtocolHeader.Write(rented.AsSpan(), MessageKind.HierarchySnapshot, 0, payload.Length);
            payload.AsSpan().CopyTo(rented.AsSpan(ProtocolConstants.HeaderSize));
            var sendBuf = new byte[total];
            rented.AsSpan(0, total).CopyTo(sendBuf);
            ArrayPool<byte>.Shared.Return(rented);
            _ingest.EnqueueSend(sendBuf);
        }

        private void TryCaptureFrame()
        {
            if (_ingest == null || targetCamera == null)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (now < _nextFrameUtc)
            {
                return;
            }

            var minDt = maxFrameRate > 0.01f ? 1f / maxFrameRate : 0.1f;
            _nextFrameUtc = now + minDt;
            EnsureRenderTexture();
            if (_rt == null)
            {
                return;
            }

            var prev = targetCamera.targetTexture;
            targetCamera.targetTexture = _rt;
            targetCamera.Render();
            targetCamera.targetTexture = prev;

            AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32, OnReadback);
        }

        private void EnsureRenderTexture()
        {
            if (targetCamera == null)
            {
                return;
            }

            var w = targetCamera.pixelWidth;
            var h = targetCamera.pixelHeight;
            if (w <= 0 || h <= 0)
            {
                return;
            }

            if (captureMaxWidth > 0 && w > captureMaxWidth)
            {
                var scale = captureMaxWidth / (float)w;
                w = captureMaxWidth;
                h = Mathf.Max(1, Mathf.RoundToInt(h * scale));
            }

            if (_rt != null && _rt.width == w && _rt.height == h)
            {
                return;
            }

            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }

            if (_readTex != null)
            {
                Destroy(_readTex);
                _readTex = null;
            }

            _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            _rt.Create();
        }

        private void OnReadback(AsyncGPUReadbackRequest request)
        {
            if (request.hasError || _ingest == null)
            {
                return;
            }

            var data = request.GetData<byte>();
            var w = request.width;
            var h = request.height;
            if (_readTex == null || _readTex.width != w || _readTex.height != h)
            {
                if (_readTex != null)
                {
                    Destroy(_readTex);
                }

                _readTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            }

            var na = new NativeArray<byte>(data.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            na.CopyFrom(data);
            _readTex.LoadRawTextureData(na);
            na.Dispose();
            _readTex.Apply(false, false);
            var jpg = ImageConversion.EncodeToJPG(_readTex, jpegQuality);

            var frameIndex = (ulong)Time.frameCount;
            var payloadLen = 4 + 4 + 8 + jpg.Length;
            var total = ProtocolConstants.HeaderSize + payloadLen;
            var sendBuf = new byte[total];
            ProtocolHeader.Write(sendBuf.AsSpan(), MessageKind.FrameJpeg, 0, payloadLen);
            FrameJpegPayload.Write(sendBuf.AsSpan(ProtocolConstants.HeaderSize), w, h, frameIndex, jpg);
            _ingest.EnqueueSend(sendBuf);
        }

        private void OnIngestBinary(ReadOnlyMemory<byte> data)
        {
            if (_ingest == null)
            {
                return;
            }

            var span = data.Span;
            if (!ProtocolHeader.TryRead(span, out var header, out var hl))
            {
                return;
            }

            if (header.Kind != MessageKind.InspectorRequest)
            {
                return;
            }

            var pl = span.Slice(hl, header.PayloadLength);
            if (!InspectorRequestPayload.TryRead(pl, out var instanceId, out _))
            {
                return;
            }

            if (!InspectorRuntimeSerializer.TryBuildResponse(instanceId, _instanceMap, header.RequestId, out var frame))
            {
                return;
            }

            _ingest.EnqueueSend(frame);
        }
    }
}
