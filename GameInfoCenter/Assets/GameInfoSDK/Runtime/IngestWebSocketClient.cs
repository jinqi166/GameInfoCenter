using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameInfo.Protocol;
using UnityEngine;

namespace GameInfo.Runtime
{
    internal sealed class IngestWebSocketClient : IDisposable
    {
        private readonly string _wsUri;

        private readonly ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();

        private ClientWebSocket? _socket;

        private CancellationTokenSource? _cts;

        private Task? _runTask;

        public event Action<ReadOnlyMemory<byte>>? BinaryMessageReceived;

        public IngestWebSocketClient(string wsUri)
        {
            _wsUri = wsUri;
        }

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();
            _runTask = Task.Run(() => RunAsync(_cts.Token));
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
            }

            try
            {
                _socket?.Abort();
            }
            catch
            {
            }

            try
            {
                _runTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }

            _runTask = null;
            _socket?.Dispose();
            _socket = null;
            _cts?.Dispose();
            _cts = null;

            while (_sendQueue.TryDequeue(out var b))
            {
                ArrayPool<byte>.Shared.Return(b);
            }
        }

        public void EnqueueSend(byte[] ownedBuffer)
        {
            _sendQueue.Enqueue(ownedBuffer);
        }

        public void Dispose()
        {
            Stop();
        }

        private static byte[] BuildSessionHelloFrame()
        {
            var nameBytes = Encoding.UTF8.GetBytes("UnityGameInfoSDK");
            var payloadLen = 4 + nameBytes.Length;
            var payloadBuf = new byte[payloadLen];
            SessionHelloPayload.Write(payloadBuf, nameBytes);
            var total = ProtocolConstants.HeaderSize + payloadLen;
            var frame = new byte[total];
            ProtocolHeader.Write(frame.AsSpan(), MessageKind.SessionHello, 0, payloadLen);
            payloadBuf.AsSpan().CopyTo(frame.AsSpan(ProtocolConstants.HeaderSize));
            return frame;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            _socket = new ClientWebSocket();
            try
            {
                await _socket.ConnectAsync(new Uri(_wsUri), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("GameInfo ingest connect failed: " + ex.Message);
                return;
            }

            var hello = BuildSessionHelloFrame();
            await _socket.SendAsync(hello, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);

            var scratch = new byte[1024 * 1024];

            while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                while (_sendQueue.TryDequeue(out var toSend))
                {
                    try
                    {
                        await _socket.SendAsync(toSend, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(toSend);
                    }
                }

                WebSocketReceiveResult result;
                var offset = 0;
                do
                {
                    if (offset >= scratch.Length)
                    {
                        Debug.LogWarning("GameInfo ingest receive buffer overflow");
                        return;
                    }

                    result = await _socket.ReceiveAsync(scratch.AsMemory(offset), cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    if (result.MessageType != WebSocketMessageType.Binary)
                    {
                        offset = 0;
                        break;
                    }

                    offset += result.Count;
                }
                while (!result.EndOfMessage);

                if (offset == 0)
                {
                    continue;
                }

                var copy = new byte[offset];
                scratch.AsSpan(0, offset).CopyTo(copy);
                var mem = new ReadOnlyMemory<byte>(copy);
                MainThreadDispatcher.Enqueue(() => BinaryMessageReceived?.Invoke(mem));
            }
        }
    }
}
