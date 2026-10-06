# Security Notes

## Network Exposure

| Port | Exposed to | Purpose |
|---|---|---|
| 5000 | Internal LAN only | SignalR + REST API |
| 5100 | Public (via Nginx) | ChatBot |
| 443 | Public | Nginx HTTPS reverse proxy |
| 3306 | Internal only | MySQL — never public |
| 3389 | Admin only | RDP management |

## Credentials

- All credentials (DB passwords, OpenAI API keys) are stored in `appsettings.json` on the server only — never committed to source control.
- `appsettings.json` is listed in `.gitignore`.
- Template files with placeholder values (`appsettings.template.json`) are committed for reference.

## OT Cybersecurity (Planned)

The relay network (IEC 61850) is an Operational Technology (OT) network. The planned architecture introduces a **data diode / forwarder PC** to enforce one-way data flow from the OT network to the IT monitoring system — preventing any internet-connected application from reaching back into the relay network.

```
OT Network (Relay)
      │  (one-way)
      ▼
Forwarder PC (data diode)
      │
      ▼
IT Network → GridLog Server → Internet
```

This is not yet implemented; the current pilot connects directly via LAN.

## Authentication

- Operators log in with a PIN. PINs are hashed (bcrypt) before storage.
- The ChatBot is public and requires no authentication.
- The web dashboard (`GridLog.Web`) currently has no login — intended for internal LAN access only.

## HTTPS

- ChatBot is served via Nginx with TLS (Let's Encrypt planned).
- The browser chat widget requires HTTPS if embedded in an HTTPS page (mixed-content policy).
- The internal SignalR API runs over plain HTTP on LAN — HTTPS is planned for production.

## Data Privacy

- No personal data of the public is stored. The ChatBot only reads grid event data.
- Operator names and PINs are stored but not exposed via any public API.
