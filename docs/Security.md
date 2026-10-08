# Security Notes

> Labels: **IMPLEMENTED** = present in code · **PLANNED** = agreed next step · **RECOMMENDED** = not yet scheduled

---

## Network Exposure

### Current (VPN-controlled development)

| Port | Exposed to | Purpose | Status |
|---|---|---|---|
| 5000 | VPN/LAN only | SignalR + REST API (Kestrel direct) | IMPLEMENTED (dev) |
| 5100 | VPN/LAN only | ChatBot (Kestrel direct) | IMPLEMENTED (dev) |
| 3306 | Internal only | MySQL — never public | IMPLEMENTED |
| 3389 | Admin/VPN only | RDP management | IMPLEMENTED |

### Target production architecture

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
            http://localhost:5000  (internal only)
                      |
                   MySQL
              localhost:3306  (private only)
```

| Port | Exposed to | Purpose | Status |
|---|---|---|---|
| 443 | Public internet | NGINX HTTPS reverse proxy | PLANNED |
| 80 | Public internet | HTTP → HTTPS redirect (optional) | PLANNED |
| 5000 | localhost only | ASP.NET Core (behind NGINX) | PLANNED |
| 5100 | localhost only | ChatBot (behind NGINX) | PLANNED |
| 3306 | localhost only | MySQL | IMPLEMENTED |
| 3389 | Admin/VPN only | RDP — must NOT be public | IMPLEMENTED |

---

## NGINX Reverse Proxy (PLANNED)

When IT assigns a public IP and domain, NGINX will:
- Terminate TLS (Let's Encrypt certificate)
- Route `/api` and `/gridHub` → `http://localhost:5000` (SignalR server)
- Route `/` → GridLog.Web
- Never expose MySQL or internal ports

### Required NGINX configuration (template — fill in domain)

```nginx
server {
    listen 80;
    server_name yourdomain.example.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    server_name yourdomain.example.com;

    ssl_certificate     /etc/letsencrypt/live/yourdomain.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yourdomain.example.com/privkey.pem;

    # SignalR + REST API
    location /api {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }

    # SignalR WebSocket hub — WebSocket upgrade is required
    location /gridHub {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade    $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 3600s;
    }

    # GridLog.Web
    location / {
        proxy_pass         http://localhost:5200;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

---

## Forwarded Headers (ASP.NET Core)

**IMPLEMENTED** (code change 2026-10-07 — controlled by configuration flag)

When `ReverseProxy:Enabled` is `true` in `appsettings.Production.json`, the server processes
`X-Forwarded-For` and `X-Forwarded-Proto` headers so that:
- Real client IPs are logged correctly
- HTTPS scheme is detected correctly behind NGINX
- SignalR WebSocket upgrade works correctly

This flag is `false` by default so the development/VPN environment is unaffected.

---

## CORS

**IMPLEMENTED** (code change 2026-10-07 — environment-specific)

| Environment | Behavior |
|---|---|
| Development | `AllowAnyOrigin()` — unchanged, VPN dev continues to work |
| Production | `WithOrigins(...)` from `Cors:AllowedOrigins` config — fail-safe deny-all if list is empty |

Production CORS origins must be set in `appsettings.Production.json` on the server
(not committed to Git — see Secrets section below).

---

## Credentials and Secrets

**IMPLEMENTED**

- All secrets (`DB passwords`, `OpenAI API keys`, `JWT signing key`) are stored outside source control.
- `appsettings.json` (DB password) and `GridLog.ChatBot/appsettings.json` (OpenAI key) are gitignored.
- `notifier.config.json` (contains ServicePin on deployed substation PCs) is gitignored.
- `appsettings.template.json` files (committed) contain placeholder values for developer guidance.
- `appsettings.Production.json` (committed) contains only non-secret structural values — no signing key, no passwords.

**Production secrets must be supplied as environment variables** (not in any file on disk):

```
ConnectionStrings__MyDbConn=server=localhost;user=gridlog_app;password=...;database=gridlog;port=3306;
OpenAI__ApiKey=sk-...
Jwt__SigningKey=<minimum 32-character random secret — generate once, keep on server only>
```

`Jwt__SigningKey` must be set before the SignalR service is started in production.
Without it, `POST /api/auth/login` returns `503` and no client can authenticate.

**RECOMMENDED (not yet done)** — Replace MySQL `root` account with a least-privilege application account:

```sql
CREATE USER 'gridlog_app'@'localhost' IDENTIFIED BY 'strong_password';
GRANT SELECT, INSERT, UPDATE, DELETE ON gridlog.* TO 'gridlog_app'@'localhost';
-- Do NOT grant DROP, CREATE, or GRANT.
```

---

## Authentication and Authorization

**IMPLEMENTED** (code change 2026-10-08) — JWT Bearer authentication is in place across all components.

### Authentication flow

```
Operator enters PIN
  → POST /api/auth/login  { "pin": "…" }   (body — PIN never in URL)
  → Server verifies PIN against operators table
  → Issues signed JWT (HMAC-SHA256, 8-hour expiry)
  → Client stores token in memory only (never written to disk or config)
  → All subsequent API calls send:  Authorization: Bearer <token>
  → SignalR WebSocket upgrade sends: /gridHub?access_token=<token>
```

### Authorization controls

| Control | Status |
|---|---|
| `POST /api/auth/login` — issues JWT | IMPLEMENTED |
| Global `[Authorize]` filter — all API endpoints require JWT by default | IMPLEMENTED |
| `[AllowAnonymous]` on public-read endpoints (see below) | IMPLEMENTED |
| `[Authorize]` on SignalR GridHub | IMPLEMENTED |
| JWT signing key — minimum 32 characters, never committed to Git | IMPLEMENTED |
| JWT signing key supplied via environment variable `Jwt__SigningKey` | IMPLEMENTED (server) / PLANNED (set on VM) |
| `POST /api/operator/verify` — in-form operator identity check (shutdown close) | IMPLEMENTED — requires JWT |
| `GET /api/operator/pin/{pin}` — legacy PIN-in-URL endpoint | **REMOVED** (2026-10-08) |
| Blazor Web login page (`/login`) | IMPLEMENTED |
| Blazor navigation guard — unauthenticated requests redirect to `/login` | IMPLEMENTED |
| WPF token expiry — app restart required after 8-hour session | KNOWN LIMITATION — re-auth UX planned |
| Notifier service — authenticates with ServicePin from `notifier.config.json` | IMPLEMENTED |
| `notifier.config.json` gitignored (contains real ServicePin on deployed PCs) | IMPLEMENTED |
| ChatBot — no authentication (public read-only endpoints) | IMPLEMENTED by design |
| PIN hashing (bcrypt) | PLANNED — Phase 2, after JWT validation in production |

### Anonymous (public) endpoints

Only three endpoints are accessible without a JWT:

| Endpoint | Reason |
|---|---|
| `POST /api/auth/login` | Login endpoint itself — must be reachable without a token |
| `GET /api/tripping/pending` | ChatBot reads this publicly for outage status queries |
| `GET /api/shutdown/pending` | ChatBot reads this publicly for outage status queries |

### JWT configuration

| Setting | Development | Production |
|---|---|---|
| Issuer | `gridlog-dev` | `gridlog-server` |
| Audience | `gridlog-clients` | `gridlog-clients` |
| Signing key | In `appsettings.Development.json` (dev-only placeholder, committed) | Environment variable `Jwt__SigningKey` — never in any file |
| Expiry | 8 hours | 8 hours |

Tokens from the development issuer (`gridlog-dev`) are rejected by a production server (issuer mismatch), and vice versa.

### PIN storage

PINs are stored as **plaintext** in the `operators.pin` column. This is a known pre-existing issue.
The `operators` table `pin` column holds the raw value; the verify endpoint does a direct equality check.
PIN hashing (bcrypt lazy migration — no forced resets) is scheduled as Phase 2 security work.

---

## SQL Injection

**IMPLEMENTED** — All user-supplied values use parameterized queries (`@param` with `AddWithValue`).
Integer ID concatenation in `IN (...)` clauses uses values retrieved from prior DB reads
(not user input) and is safe. No SQL injection vulnerability was found in the review.

---

## ChatBot Isolation

**IMPLEMENTED** — The ChatBot only calls:
- `GET /api/tripping/pending`
- `GET /api/shutdown/pending`
- `GET /api/bay` (feeder status lookup)

These are read-only operations. The ChatBot has no write access to any operational data.
OpenAI function-calling uses an explicit allowlist of three functions:
`get_feeder_status`, `get_all_outages`, `get_no_light_contact`.
Model-generated text is never executed as SQL or shell commands.

---

## OT / IT Security

**PLANNED** — The relay network (IEC 61850) is an Operational Technology (OT) network.
The planned architecture introduces an **OT-to-IT one-way data forwarding gateway** to enforce
one-way data flow from the OT network to the IT monitoring system — preventing any
internet-connected application from initiating arbitrary connections into the relay network.

```
OT Network (Relay / IEC 61850)
      │  (one-way forwarding — no reverse path)
      ▼
OT-to-IT Forwarding Gateway
      │
      ▼
IT Network → GridLog Server → NGINX → Internet
```

This is not yet implemented. The current pilot connects directly via LAN.
The gateway is **not** a true hardware data diode unless enforced one-way communication
is implemented at the physical/hardware layer. Use the term "OT-to-IT forwarding gateway"
until hardware enforcement is confirmed.

---

## Error Handling

**RECOMMENDED** — Production API responses must not expose:
- Stack traces
- SQL statements or table names
- Server filesystem paths
- Internal network details

ASP.NET Core's default behavior in Production mode (non-Development) suppresses
developer exception pages. Verify `ASPNETCORE_ENVIRONMENT=Production` is set on the server.

---

## Backup and Recovery

**RECOMMENDED — not yet implemented**

| Item | Recommendation |
|---|---|
| MySQL backup | `mysqldump gridlog` daily minimum |
| Backup frequency | Daily full + transaction logs if InnoDB |
| Backup storage | Off-server — network share or cloud storage |
| Retention | Minimum 30 days |
| Restoration procedure | Documented and tested before production cutover |
| Restoration testing | Test restore to a separate instance quarterly |

No backup mechanism is currently implemented in the deployment.
