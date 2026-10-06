# Deployment Guide

## Server Requirements

- Windows Server 2022
- 4 vCPU, 8 GB RAM, 100 GB SSD
- MySQL 8.0
- IIS (for GridLog.Web)
- .NET 10 Runtime

## Services on the Server

Both server components run as **native Windows Services** (no NSSM):

| Service Name | Binary | Port |
|---|---|---|
| GridLog.SignalR | `signalr\WPFGridLog-1.SignalR.exe` | 5000 |
| GridLog.ChatBot | `chatbot\GridLog.ChatBot.exe` | 5100 |

### Install a service
```powershell
sc.exe create GridLog.SignalR `
  binPath= "C:\gridlog-deploy\signalr\WPFGridLog-1.SignalR.exe --contentRoot C:\gridlog-deploy\signalr" `
  start= auto

sc.exe start GridLog.SignalR
```

### Kestrel must listen on all interfaces
`appsettings.json` on the server must include:
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://0.0.0.0:5000" }
    }
  }
}
```

## IIS (GridLog.Web)

- Physical path: `C:\gridlog-deploy\web\`
- `web.config` at root rewrites requests into `wwwroot\`
- `BlazorEnableCompression=false` in the project prevents IIS integrity hash mismatch with pre-compressed Blazor assets
- Runtime API URL configured in `web\wwwroot\appsettings.json`:
  ```json
  { "ApiBaseUrl": "http://<server-ip>:5000" }
  ```

## Desktop App (Substation PCs)

- WPFGridLog-1 and GridLog.Notifier are distributed as installers (Inno Setup — in progress)
- `App.config` on each PC sets the server URL:
  ```xml
  <add key="ApiBaseUrl" value="http://<server-ip>:5000"/>
  ```

## Deployment Workflow

Source is built on the developer laptop and pushed to a separate deployment repository (binaries only, no source code). The server pulls from this repo:

```
Developer laptop
  dotnet publish → C:\publish\signalr\
  git push → GridLog-Deploy (GitHub)
                    │
                    ▼
           Server: git pull
           Restart-Service GridLog.SignalR
```

## MySQL Timezone

MySQL timezone must match the server OS timezone (Nepal Standard Time, UTC+05:45):

```powershell
Set-TimeZone -Id "Nepal Standard Time"
Restart-Service MySQL80
```
