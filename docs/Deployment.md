# Deployment Guide

> Labels: **IMPLEMENTED** = in place · **PLANNED** = agreed next step · **RECOMMENDED** = not yet scheduled

---

## Server Requirements

- Windows Server 2022
- 4 vCPU, 8 GB RAM, 100 GB SSD (minimum)
- MySQL Community Server 8.0
- .NET 10 Runtime (or self-contained publish — see below)
- NGINX (planned, for production internet exposure)

---

## Current Development Architecture

```
Substation PC (VPN)                Central Server VM (192.168.8.141)
──────────────────                 ─────────────────────────────────
WPFGridLog-1 ────── HTTP:5000 ──►  WPFGridLog-1.SignalR (port 5000)
GridLog.Notifier                   GridLog.ChatBot      (port 5100)
                                   GridLog.Web
                                   MySQL (localhost:3306 — private)
```

VPN is required to reach the VM. No public internet exposure exists in the current phase.

---

## Target Production Architecture

```
                  INTERNET
                      |
                  HTTPS :443
                      |
                   NGINX
                      |
         +------------+-----------+
         |            |           |
        /api       /gridHub       /
         |            |           |
         +------------+-----------+
                      |
               ASP.NET Core
           http://localhost:5000
                      |
                   MySQL
              localhost:3306 (private)
```

WPF desktop clients and authorized web users communicate through authenticated HTTPS.
SignalR provides real-time notification only; the database is the authoritative data source.

---

## Firewall Port Table

| Port | Protocol | Direction | Source | Destination | Purpose | Status |
|---|---|---|---|---|---|---|
| 443 | TCP | Inbound | Internet | NGINX | HTTPS reverse proxy | PLANNED |
| 80 | TCP | Inbound | Internet | NGINX | HTTP → HTTPS redirect | PLANNED |
| 5000 | TCP | Internal | localhost | ASP.NET Core | SignalR + REST API | IMPLEMENTED (dev) |
| 5100 | TCP | Internal | localhost | ASP.NET Core | ChatBot | IMPLEMENTED (dev) |
| 3306 | TCP | Internal | localhost | MySQL | Database | IMPLEMENTED — NEVER expose publicly |
| 3389 | TCP | Inbound | VPN/admin | Server | RDP management | IMPLEMENTED — VPN only |

Development only (NOT for production internet):
- Port 5000 may be temporarily accessible over VPN during the development phase.
- This must be blocked from the public internet before production cutover.

---

## Services on the Server

Both server components run as **native Windows Services** (installed with `sc.exe`):

| Service Name | Binary | Internal Port |
|---|---|---|
| GridLog.SignalR | `signalr\WPFGridLog-1.SignalR.exe` | 5000 |
| GridLog.ChatBot | `chatbot\GridLog.ChatBot.exe` | 5100 |

### Install a service

```powershell
sc.exe create GridLog.SignalR `
  binPath= "C:\gridlog-deploy\signalr\WPFGridLog-1.SignalR.exe" `
  start= auto

sc.exe start GridLog.SignalR
```

---

## Configuration

### Development (VPN)

`appsettings.json` on the server (gitignored — never committed):

```json
{
  "ConnectionStrings": {
    "MyDbConn": "server=localhost;user=root;password=REAL_PASSWORD;database=gridlog;port=3306;"
  }
}
```

Kestrel listens on all interfaces so VPN clients can reach it:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://0.0.0.0:5000" }
    }
  }
}
```

### Production (NGINX)

`appsettings.Production.json` is committed and contains only non-secret values:

```json
{
  "Cors": {
    "AllowedOrigins": [ "https://yourdomain.example.com" ]
  },
  "ReverseProxy": {
    "Enabled": true
  },
  "Jwt": {
    "Issuer": "gridlog-server",
    "Audience": "gridlog-clients",
    "ExpirationHours": 8
  }
}
```

**Secrets** must be supplied as environment variables on the server (not in any file):

```
ConnectionStrings__MyDbConn=server=localhost;user=gridlog_app;password=REAL_PASSWORD;database=gridlog;port=3306;
OpenAI__ApiKey=sk-...
Jwt__SigningKey=REPLACE_WITH_STRONG_RANDOM_SECRET_MINIMUM_32_CHARACTERS
```

`Jwt__SigningKey` is required. Without it, `POST /api/auth/login` returns `503` and
no WPF client, Notifier, or Blazor Web user can authenticate.

In production, Kestrel should listen on localhost only (NGINX handles external traffic):

```
ASPNETCORE_URLS=http://localhost:5000
ASPNETCORE_ENVIRONMENT=Production
```

### Notifier service configuration (substation PCs)

Each substation PC running GridLog.Notifier needs a `notifier.config.json` file alongside the binary:

```json
{
  "HubUrl": "https://yourdomain.example.com",
  "ServicePin": "REPLACE_WITH_OPERATOR_PIN"
}
```

`notifier.config.json` is gitignored — it contains a real operator PIN and must never be committed.
Use `notifier.config.template.json` (committed) as the reference template.

---

## Publish and Deploy

### 1. Publish (developer laptop — disconnect VPN first for `git push`)

```powershell
cd C:\Users\ranje\source\repos\GridLog\GridLog

# Self-contained publish — required because VM LocalSystem account has no dotnet in PATH
dotnet publish WPFGridLog-1.SignalR/WPFGridLog-1.SignalR.csproj -c Release `
    -o C:\publish\signalr --self-contained -r win-x64

dotnet publish GridLog.ChatBot/GridLog.ChatBot.csproj -c Release `
    -o C:\publish\chatbot --self-contained -r win-x64

cd C:\publish
git add signalr/ chatbot/
git commit -m "deploy: ..."
git push   # VPN must be OFF for GitHub push
```

### 2. VM auto-deploy (GitHub Actions self-hosted runner)

A self-hosted runner on the VM (`C:\actions-runner\`) listens for pushes to `main`.
The workflow (`.github/workflows/deploy.yml`) runs:

```
1. Stop-Service GridLog.ChatBot; Stop-Service GridLog.SignalR  ← MUST stop BEFORE pull
2. git -C C:\gridlog-deploy pull
3. Start-Service GridLog.ChatBot; Start-Service GridLog.SignalR
```

**Important:** Services must stop before `git pull` to release DLL file locks.

---

## Desktop App (Substation PCs)

WPFGridLog-1 and GridLog.Notifier are configured via `App.config`:

```xml
<!-- Development / VPN: -->
<add key="ApiBaseUrl" value="http://192.168.8.141:5000"/>

<!-- Production (after NGINX + TLS): -->
<add key="ApiBaseUrl" value="https://yourdomain.example.com"/>
```

Installers (Inno Setup) are planned. Distribution is currently manual copy.

---

## GridLog.Web (Blazor)

API base URL is configured in `wwwroot/appsettings.json`:

```json
{ "ApiBaseUrl": "http://localhost:5000" }
```

Update to `https://yourdomain.example.com` before production internet exposure.

---

## Database

### Timezone

MySQL timezone must match the server OS (Nepal Standard Time, UTC+05:45):

```powershell
Set-TimeZone -Id "Nepal Standard Time"
Restart-Service MySQL80
```

### Least-Privilege Application Account (RECOMMENDED — not yet done)

The current deployment uses the MySQL `root` account. Before production internet exposure,
create a dedicated application account:

```sql
CREATE USER 'gridlog_app'@'localhost' IDENTIFIED BY 'strong_password';
GRANT SELECT, INSERT, UPDATE, DELETE ON gridlog.* TO 'gridlog_app'@'localhost';
FLUSH PRIVILEGES;
```

Do **not** grant `DROP`, `CREATE`, `ALTER`, or `GRANT` privileges to the application account.

### Schema Migrations

Schema migrations run automatically at server startup (`Program.cs`).
They are safe to run repeatedly (idempotent DDL with `IF NOT EXISTS` and `INFORMATION_SCHEMA` checks).

### Backup (RECOMMENDED — not yet implemented)

```powershell
# Example daily backup script (schedule with Task Scheduler)
$date = Get-Date -Format "yyyyMMdd"
mysqldump --user=gridlog_backup --password=BACKUP_PASSWORD gridlog `
    > "D:\backups\gridlog_$date.sql"
```

Minimum recommended:
- Daily full backup
- Off-server copy (network share or cloud storage)
- 30-day retention
- Restoration tested before production cutover

---

## Disaster Recovery

| Scenario | Recovery action | Status |
|---|---|---|
| Service crash | Windows Service auto-restart (configure `sc.exe failure`) | RECOMMENDED |
| Server reboot | Services set to `start= auto` restart automatically | IMPLEMENTED |
| Database corruption | Restore from most recent `mysqldump` backup | RECOMMENDED (backup not yet implemented) |
| Deployment rollback | `git -C C:\gridlog-deploy checkout <previous-commit>` then restart services | IMPLEMENTED |
| OpenAI API key compromised | Rotate key at platform.openai.com, update server environment variable | RECOMMENDED process |

---

## go-live Checklist

Before exposing the server to the public internet:

**Authentication (JWT)**
- [ ] `Jwt__SigningKey` environment variable set on server (min 32 chars, cryptographically random)
- [ ] Dedicated Notifier operator account created in DB; ServicePin added to `notifier.config.json` on each substation PC
- [ ] Verified `POST /api/auth/login` returns `200` with a token (not `503`)
- [ ] Verified WPF login works end-to-end over HTTPS

**Server configuration**
- [ ] Public IP and domain name assigned by IT
- [ ] NGINX installed and HTTPS configured (Let's Encrypt)
- [ ] `ASPNETCORE_ENVIRONMENT=Production` set on server
- [ ] `ReverseProxy:Enabled: true` in `appsettings.Production.json`
- [ ] CORS `AllowedOrigins` set to actual production domain
- [ ] `ASPNETCORE_URLS=http://localhost:5000` (Kestrel localhost only)

**Secrets and access**
- [ ] DB password, OpenAI key, JWT signing key all in environment variables — not in any file
- [ ] Port 5000 blocked from public internet at firewall
- [ ] Port 3306 confirmed not reachable from public internet
- [ ] RDP (3389) confirmed accessible via VPN only

**Database**
- [ ] MySQL root → `gridlog_app` least-privilege account migration

**Operations**
- [ ] Backup mechanism tested and verified
- [ ] Service auto-restart configured (`sc.exe failure`)
