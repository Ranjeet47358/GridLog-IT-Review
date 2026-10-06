# Architecture Overview

## System Topology

```
Substation PC (each substation)          Central Server (VM)
────────────────────────────             ─────────────────────────────
WPFGridLog-1 (WPF Desktop)   ◄──────►  WPFGridLog-1.SignalR  (:5000)
GridLog.Notifier (AppBar)    SignalR     GridLog.ChatBot       (:5100)
                                         GridLog.Web  (IIS)
MySQL (local — future)  ──sync TBD──►  MySQL (central)
                                                │
                                         Nginx (HTTPS :443)
                                                │
                                         Public Internet
                                                │
                                   NEA Customer Care Website
                                   (chat-widget.js embedded)
```

---

## Component Details

### WPFGridLog-1 (Substation Desktop App)
- Built with WPF (.NET 10, Windows only)
- Operator logs tripping events (relay type, fault type, phase, current) and planned shutdowns
- Dates entered in **Bikram Sambat (BS)** calendar — converted to AD internally via XML lookup table
- Connects to central server via SignalR for real-time synchronisation
- Plays audible alarm on new tripping event
- Reads relay auto-trip events from IEC 61850 relay (via `GridLog.Notifier`)

### GridLog.Notifier (Windows AppBar)
- Reserves a strip at the top of the Windows screen (SHAppBarMessage Win32 API)
- Shows live cards for each active trip/shutdown with elapsed time counter
- Rings bell alert (Win32 `Beep`) on new events; muted when operator is already on the Tripping page
- Connects to SignalR hub to receive `PendingChanged` broadcasts

### WPFGridLog-1.SignalR (API + Hub Server)
- ASP.NET Core 10 running as a **Windows Service** (`UseWindowsService()`)
- Listens on `http://0.0.0.0:5000`
- Exposes REST API endpoints (`/api/tripping`, `/api/shutdown`, `/api/bay`, etc.)
- SignalR hub at `/gridHub` — broadcasts `PendingChanged` to all connected clients when data changes
- Raw SQL via `MySql.Data` — no ORM

### GridLog.ChatBot (Public Chatbot)
- ASP.NET Core 10 running as a **Windows Service**
- Listens on `http://0.0.0.0:5100`
- OpenAI `gpt-4o-mini` with function calling:
  - `get_feeder_status` — checks if a feeder is tripped or under shutdown
  - `get_all_outages` — lists current outages
  - `get_no_light_contact` — returns the No Light office contact for a feeder
- Dates returned in BS calendar format (Nepali users expect BS)
- Embeddable on any website via `<script src=".../chat-widget.js">`

### GridLog.Web (Web Dashboard)
- Blazor WebAssembly (.NET 10) hosted on IIS
- Reads from the SignalR server REST API
- Cross-substation event viewer — tripping history, shutdown history, KPIs
- Config (`ApiBaseUrl`) injected at runtime via `wwwroot/appsettings.json`

---

## Real-Time Flow

```
Operator saves trip on WPFGridLog-1
        │
        ▼
POST /api/tripping  →  SignalR Server
        │                    │
        │              MySQL INSERT
        │                    │
        │           hub.Clients.All
        │           .SendAsync("PendingChanged")
        │                    │
        ├────────────────────┤
        ▼                    ▼
WPFGridLog-1          GridLog.Notifier
refreshes pending     shows new card +
panel                 rings bell
```

---

## Nepali Calendar (BS) Handling

All dates displayed to operators are in **Bikram Sambat (BS)**. The system:
1. Stores all datetimes in MySQL as AD (Gregorian) with no timezone offset (`DateTimeKind.Unspecified`)
2. Converts AD → BS for display using an XML lookup table (`NepaliCalander.xml`)
3. Converts BS → AD when operator enters a date before saving

This avoids timezone ambiguity across the client–server boundary.
