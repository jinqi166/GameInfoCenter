using System.Net.WebSockets;
using System.Text;
using GameInfo.Protocol;
using GameInfoWebServer.Models;
using GameInfoWebServer.Services;

namespace GameInfoWebServer.Endpoints;

public static class SessionEndpoints
{
    public static void MapGameInfoEndpoints(this WebApplication app)
    {
        app.MapPost("/api/sessions", (GameSessionStore store) =>
        {
            var session = store.CreateSession();
            var response = new CreateSessionResponse
            {
                SessionId = session.SessionId,
                IngestToken = session.IngestToken,
                ViewerToken = session.ViewerToken,
                IngestWebSocketPath = "/ws/ingest",
                ViewerWebSocketPath = "/ws/view",
                LivePagePath = $"/live/{session.SessionId}",
                RecommendedHeartbeatIntervalMs = 5000,
            };
            return Results.Json(response);
        });

        app.MapPost("/api/sessions/{sessionId}/heartbeat", (string sessionId, HeartbeatRequest body, GameSessionStore store) =>
        {
            if (!store.TryGet(sessionId, out var session) || session is null)
            {
                return Results.NotFound();
            }

            session.LastHeartbeatUtc = DateTimeOffset.UtcNow;
            return Results.Json(new { ok = true, serverUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), sequence = body.Sequence });
        });

        app.MapGet("/api/sessions/{sessionId}/heartbeat", (string sessionId, ulong? sequence, GameSessionStore store) =>
        {
            if (!store.TryGet(sessionId, out var session) || session is null)
            {
                return Results.NotFound();
            }

            session.LastHeartbeatUtc = DateTimeOffset.UtcNow;
            return Results.Json(new { ok = true, serverUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), sequence });
        });

        app.Map("/ws/ingest", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var store = context.RequestServices.GetRequiredService<GameSessionStore>();
            var sessionId = context.Request.Query["sessionId"].ToString();
            var token = context.Request.Query["token"].ToString();
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(token))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("sessionId and token required", Encoding.UTF8).ConfigureAwait(false);
                return;
            }

            if (!store.TryGet(sessionId, out var session) || session is null || session.IngestToken != token)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            await WebSocketSessionHandler.HandleIngestAsync(socket, session, context.RequestAborted).ConfigureAwait(false);
        });

        app.Map("/ws/view", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var store = context.RequestServices.GetRequiredService<GameSessionStore>();
            using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            await WebSocketSessionHandler.HandleViewerAsync(socket, store, context.RequestAborted).ConfigureAwait(false);
        });
    }
}
