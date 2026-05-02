# GameInfoCenter

游戏资源采集服务：Unity 客户端采集（画面、Hierarchy、Inspector）并通过 HTTP 心跳 + WebSocket 二进制协议上报到 ASP.NET Core Web 服务，在浏览器中实时查看。

## 仓库结构

| 路径 | 说明 |
|------|------|
| [GameInfoCenter/](GameInfoCenter/) | Unity 2022.3.61 工程根目录 |
| [GameInfoCenter/Assets/GameInfoSDK/](GameInfoCenter/Assets/GameInfoSDK/) | 客户端 SDK（`GameInfoCollector` 等） |
| [GameInfoCenter/Tools/GameInfo.Protocol/](GameInfoCenter/Tools/GameInfo.Protocol/) | 共享二进制协议（netstandard2.1），编译为 DLL 供 Unity 引用 |
| [GameInfoCenter/Tools/GameInfoWebServer/](GameInfoCenter/Tools/GameInfoWebServer/) | Blazor Server Web 服务（HTTP API + WebSocket + `/live` 页面） |

详细联调步骤见 [GameInfoCenter/Tools/GameInfoWebServer/README.md](GameInfoCenter/Tools/GameInfoWebServer/README.md)。

## 协议 DLL 更新

修改协议后请在仓库根目录执行：

```bash
dotnet build -c Release GameInfoCenter/Tools/GameInfo.Protocol/GameInfo.Protocol.csproj
cp GameInfoCenter/Tools/GameInfo.Protocol/bin/Release/netstandard2.1/GameInfo.Protocol.dll GameInfoCenter/Assets/GameInfoSDK/Runtime/GameInfo.Protocol.dll
```

然后在 Unity 中重新导入资源。
