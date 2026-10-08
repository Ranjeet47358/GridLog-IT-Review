# REST API Reference

Base URL: `http://<server>:5000`

All endpoints return JSON. Datetimes are ISO 8601 without timezone offset (Nepal local time).

---

## Authentication

### POST `/api/auth/login`

Verifies an operator PIN and returns a signed JWT. No `Authorization` header required.

**Body:**
```json
{ "pin": "1234" }
```

**Response `200 OK`:**
```json
{
  "token": "<jwt>",
  "expiresAt": "2026-10-09T06:00:00.0000000Z",
  "operatorId": 3,
  "operatorName": "Ram Bahadur",
  "designation": "Operator"
}
```

**Errors:**
- `400` — PIN missing or longer than 20 characters
- `401` — PIN not found in `operators` table
- `503` — Server signing key not configured (admin must set `Jwt__SigningKey` env var)

**Token usage** — include the token in all subsequent requests:

```
Authorization: Bearer <token>
```

For SignalR WebSocket connections (browsers cannot set `Authorization` on the WS handshake):

```
ws://<server>:5000/gridHub?access_token=<token>
```

**Token expiry:** 8 hours. WPF clients must re-enter their PIN (restart app) after expiry.
The Notifier service re-authenticates automatically on reconnect.

---

### Anonymous endpoints

The following two endpoints do **not** require a JWT (used by the public ChatBot):

- `GET /api/tripping/pending`
- `GET /api/shutdown/pending`

All other endpoints require a valid `Authorization: Bearer <token>` header.

---

## Tripping Events

### GET `/api/tripping`
List tripping records.

**Query params:**
| Param | Type | Description |
|---|---|---|
| substationId | int | filter by substation |
| bayId | int | filter by bay |
| from | date (yyyy-MM-dd) | start date |
| to | date (yyyy-MM-dd) | end date |

**Response:** array of tripping record objects (max 500).

---

### POST `/api/tripping`
Save a new tripping record.

**Body:**
```json
{
  "bayId": 8,
  "relayTypeId": 3,
  "offDateTime": "2083-10-07T00:31:00",
  "onDateTime": null,
  "remarks": "Earth fault on feeder",
  "phaseA": false,
  "phaseB": false,
  "phaseC": false,
  "phaseG": true,
  "threePhase": false,
  "I": null,
  "Ia": null,
  "Ib": null,
  "Ic": null,
  "Ig": 450.0,
  "faultType": "TEMPORARY",
  "elements": [
    { "elementId": 12, "stage": "1" }
  ]
}
```

**Returns:** `201 Created` with `{ "id": 215 }`

**Errors:**
- `409 Conflict` — duplicate OFF time or overlap with existing open record

---

### PUT `/api/tripping/{id}`
Update an existing record (e.g. add ON time on restoration).

---

### DELETE `/api/tripping/{id}`
Delete a tripping record.

---

### POST `/api/tripping/{id}/restore`
Close an open trip by providing ON time.

**Body:** `{ "onDateTime": "2026-10-07T02:15:00" }`

---

## Shutdown Events

### GET `/api/shutdown`
List shutdown records. Same query params as tripping.

### POST `/api/shutdown`
Create a shutdown record.

### PUT `/api/shutdown/{id}/close`
Close a shutdown, providing ON time and work summary.

---

## Bays

### GET `/api/bay?substationId={id}`
List bays for a substation, including relay type info.

---

## SignalR Hub

URL: `ws://<server>:5000/gridHub`

**Authentication required.** Pass the JWT as a query string parameter (browsers cannot set
`Authorization` on a WebSocket upgrade):

```
ws://<server>:5000/gridHub?access_token=<token>
```

### Server → Client events

| Event | Payload | Description |
|---|---|---|
| `PendingChanged` | — | Data changed; client should refresh pending list |
| `NavigateTo` | `"TrippingEvents"` | Tells WPFGridLog-1 to switch page |
| `ActivePageChanged` | page name | WPFGridLog-1 broadcasts its current page |

### Client → Server

Clients connect and listen only. Writes go through the REST API, which then broadcasts via the hub.

---

## ChatBot API

Base URL: `http://<server>:5100`

### POST `/api/chat`

**Body:**
```json
{
  "message": "Baneshwor feeder ko light kaha gayo?",
  "history": []
}
```

**Response:**
```json
{
  "reply": "Baneshwor (66kV) feeder हाल ट्रिप भएको छ। बन्द समय: २०८३-०६-२० ००:३१। कारण: Earth fault।"
}
```
