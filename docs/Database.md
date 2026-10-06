# Database

## Engine
- MySQL Community Server 8.0
- One instance per deployment (central server)
- Database name: `gridlog`

## Core Tables

### `substation`
Stores substation master data.

| Column | Type | Description |
|---|---|---|
| substation_id | INT PK | |
| substation_name | VARCHAR | e.g. "Patan", "Khimti" |
| location | VARCHAR | |

### `bay`
Each feeder/bay belongs to a substation.

| Column | Type | Description |
|---|---|---|
| bay_id | INT PK | |
| substation_id | INT FK | → substation |
| bay_name | VARCHAR | e.g. "Baneshwor (66kV)" |
| voltage_level | VARCHAR | e.g. "66kV", "11kV" |
| bay_type | VARCHAR | "feeder", "transformer", etc. |

### `relay_type`
Relay models installed on bays.

| Column | Type | Description |
|---|---|---|
| relay_type_id | INT PK | |
| relay_name | VARCHAR | e.g. "Schneider P3U30" |
| frame_key | VARCHAR | controls which UI frame renders |
| phase_notation | VARCHAR | "ABC" or "RST" |
| current_unit | VARCHAR | "A" or "kA" |

### `tripping_records`
Core event table — one row per trip.

| Column | Type | Description |
|---|---|---|
| id | INT PK | |
| bay_id | INT FK | |
| relay_type_id | INT FK | nullable |
| off_datetime | DATETIME | breaker opened (AD, no offset) |
| on_datetime | DATETIME | breaker restored; NULL = still open |
| total_time | INT | minutes; computed on restore |
| fault_type | VARCHAR | "TEMPORARY" / "PERMANENT" / "FORCED" |
| phase_a/b/c/g | TINYINT | which phases operated |
| three_phase | TINYINT | |
| I, Ia, Ib, Ic, Ig | DOUBLE | fault current readings |
| remarks | TEXT | |

### `shutdown_records`
Planned maintenance shutdowns.

| Column | Type | Description |
|---|---|---|
| id | INT PK | |
| bay_id | INT FK | |
| shutdown_type | VARCHAR | "EMERGENCY" / "PLANNED" |
| off_datetime | DATETIME | |
| on_datetime | DATETIME | nullable |
| total_time | INT | minutes |
| operator_id | INT FK | who opened |
| closed_by | INT FK | who closed |
| remarks | TEXT | |

### `shutdown_work_items`
Work tasks attached to a shutdown.

| Column | Type | Description |
|---|---|---|
| id | INT PK | |
| shutdown_id | INT FK | |
| work_type_id | INT FK | |
| staff_id | INT FK | DCS staff assigned |

### `operators`
Control room operators.

| Column | Type | Description |
|---|---|---|
| operator_id | INT PK | |
| operator_name | VARCHAR | |
| pin_hash | VARCHAR | hashed PIN for login |
| substation_id | INT FK | |

## Key Design Decisions

- **No timezone stored** — all datetimes are plain local Nepal time. Client sends `DateTimeKind.Unspecified` (no offset in JSON) and server stores as-is.
- **No ORM** — raw SQL via `MySql.Data` for full query control and performance visibility.
- **Overlap validation** — server rejects a new OFF time that falls inside an existing open record for the same bay.
- **Duplicate check** — server rejects exact duplicate `(bay_id, off_datetime)` pairs.

See [sample-schema.sql](../database/sample-schema.sql) for DDL.
