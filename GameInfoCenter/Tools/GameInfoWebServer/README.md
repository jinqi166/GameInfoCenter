# GameInfoWebServer

ASP.NET Core 8.0 Blazor Server 应用：提供会话 HTTP API、采集端与浏览器的 WebSocket 端点，以及 `/live/{sessionId}` 实时查看页。

## 运行

```bash
cd GameInfoCenter/Tools/GameInfoWebServer/GameInfoWebServer
dotnet run
```

默认监听见 `Properties/launchSettings.json`（通常为 `http://localhost:5000`）。

## 流程

1. 浏览器打开 `/session-setup`，点击 **Create session**，记下返回的 **SessionId**、**Ingest token**、**Viewer token**。
2. Unity 场景中添加空物体，挂载 **GameInfoCollector**，将 **Server Base Url** 设为上述地址（如 `http://localhost:5000/`），勾选 **Auto Create Session** 或手动填入 token。
3. 浏览器通过 **Open live view** 或手动访问 `/live/{SessionId}?token={ViewerToken}` 查看画面、Hierarchy 与 Inspector。

## 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/sessions` | 创建会话，返回 token 与路径提示 |
| GET/POST | `/api/sessions/{id}/heartbeat` | 心跳（Unity 默认约每 5 秒 GET） |
| WS | `/ws/ingest?sessionId=&token=` | Unity 采集端二进制上行 |
| WS | `/ws/view` | 浏览器首帧发送 `ViewerSubscribe` 载荷后订阅 |

## 开发与测试

```bash
dotnet build GameInfoCenter/Tools/GameInfoWebServer/GameInfoWebServer/GameInfoWebServer.csproj
dotnet test GameInfoCenter/Tools/GameInfo.Protocol.Tests/GameInfo.Protocol.Tests.csproj
```
