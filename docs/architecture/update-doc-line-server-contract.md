# Purpose

This document defines the canonical server contract for `UpdateDocLine` in FlowStock.

The goal is to preserve server-centric lifecycle semantics without reintroducing mutable draft-line writes in clients or in SQL.

# Canonical operation

Canonical remote path:

- `POST /api/docs/{docUid}/lines/update`

Canonical application behavior:

- validate target document and target line;
- validate that the document is still `DRAFT`;
- append a new `doc_lines` row representing the updated line state;
- link the new row to the superseded line via `replaces_line_id`;
- record an idempotency event in `api_events`;
- do not write `ledger`;
- do not change `docs.status`;
- do not mutate the original `doc_lines` row.

# Append-only update semantics

Example:

Before update:

- `doc_lines.id = 1, item=A, qty=10`

After updating qty to `12`:

- `doc_lines.id = 1, item=A, qty=10`
- `doc_lines.id = 2, item=A, qty=12, replaces_line_id = 1`

Active document projections must treat row `2` as the active line and row `1` as historical.

This implies:

- document line read-models return only active rows;
- order/draft totals that depend on `doc_lines` ignore superseded rows;
- close/ledger writing must operate on the active line set only.

# `replaces_line_id`

Field:

- `doc_lines.replaces_line_id NULL`

Meaning:

- `NULL` for original lines and for plain `AddDocLine`;
- set to the superseded `doc_lines.id` for append-only updates;
- reserved for future append-only delete/tombstone semantics as well.

Current constraints:

- target line must exist in the same document;
- target line must be active at update time;
- closed documents cannot receive replacement lines.

# Request DTO

Canonical request shape:

```json
{
  "event_id": "evt-...",
  "device_id": "WPF-01",
  "line_id": 123,
  "qty": 12,
  "uom_code": "BOX",
  "from_location_id": null,
  "to_location_id": 10,
  "from_hu": null,
  "to_hu": "HU-000123"
}
```

Required:

- `event_id`
- `line_id`
- `qty > 0`

Location/HU fields remain subject to document-type rules and may fall back to the existing line or draft header metadata.

# Response DTO

Successful update:

```json
{
  "ok": true,
  "result": "UPDATED",
  "doc_uid": "doc-uid",
  "doc_status": "DRAFT",
  "appended": true,
  "idempotent_replay": false,
  "line": {
    "id": 456,
    "replaces_line_id": 123,
    "item_id": 100,
    "qty": 12,
    "uom_code": "BOX",
    "order_line_id": null,
    "from_location_id": null,
    "to_location_id": 10,
    "from_hu": null,
    "to_hu": "HU-000123"
  }
}
```

Idempotent replay:

```json
{
  "ok": true,
  "result": "IDEMPOTENT_REPLAY",
  "doc_uid": "doc-uid",
  "doc_status": "DRAFT",
  "appended": false,
  "idempotent_replay": true,
  "line": {
    "id": 456,
    "replaces_line_id": 123
  }
}
```

# Validation rules

Canonical validation:

- `doc_uid` resolves to an existing document;
- document exists and `docs.status == DRAFT`;
- target `line_id` exists in that document and is currently active;
- `qty > 0`;
- location references, if provided, resolve to existing locations;
- HU references, if provided, resolve to usable HU records;
- effective location/HU set satisfies doc-type-specific rules.

Current failure codes:

- `EMPTY_BODY`
- `INVALID_JSON`
- `MISSING_EVENT_ID`
- `MISSING_LINE_ID`
- `INVALID_QTY`
- `DOC_NOT_FOUND`
- `DOC_NOT_DRAFT`
- `INVALID_TYPE`
- `UNKNOWN_LINE`
- `UNKNOWN_LOCATION`
- `UNKNOWN_HU`
- `MISSING_LOCATION`

# Idempotency behavior

Event type:

- `DOC_LINE_UPDATE`

Rules:

- same `event_id` + same normalized payload -> `IDEMPOTENT_REPLAY`
- same `event_id` + different normalized payload -> `EVENT_ID_CONFLICT`
- different `event_id` + same semantic line intent -> new append-only replacement row

Normalized payload currently compares:

- `doc_uid`
- `event_id`
- `device_id`
- `line_id`
- `qty`
- `uom_code`
- `from_location_id`
- `to_location_id`
- `from_hu`
- `to_hu`

# Logging

Structured lifecycle log uses:

- `operation=UpdateDocLine`
- `result=UPDATED | IDEMPOTENT_REPLAY | EVENT_ID_CONFLICT | VALIDATION_FAILED`
- `doc_uid`
- `doc_id`
- `doc_ref`
- `doc_type`
- `line_id`
- `replaces_line_id`
- `event_id`
- `device_id`
- `appended`
- `idempotent_replay`

Example:

```text
doc_lifecycle operation=UpdateDocLine path=/api/docs/tsd-doc-001/lines/update result=UPDATED doc_uid=tsd-doc-001 doc_id=412 doc_ref=IN-2026-000123 doc_type=INBOUND doc_status_before=DRAFT doc_status_after=DRAFT line_count=3 line_id=955 replaces_line_id=701 ledger_rows_written=0 event_id=evt-line-update-001 device_id=WPF-01 api_event_written=True appended=True idempotent_replay=False already_closed= elapsed_ms=9 errors=
```

# WPF migration boundary

WPF server mode uses:

- `WpfUpdateDocLineService`
- `UpdateDocLineApiClient`

Current migration boundary:

- manual quantity edits can use the canonical update endpoint under feature flag;
- legacy local `UpdateDocLineQty(...)` remains available as rollback path;
- delete/split/HU reassignment paths remain outside this contract.

# Non-goals

This contract does not change:

- `POST /api/docs/{docUid}/lines` add-line semantics;
- `POST /api/docs/{docUid}/close` close semantics;
- batch line fills;
- JSONL import;
- legacy local line delete behavior.

# Decisions requiring confirmation

- resolved later: delete is also append-only via a tombstone row with `qty = 0` and `replaces_line_id = deleted_line_id`; see `delete-doc-line-server-contract.md`;
- whether `qty_input` should become part of the canonical update payload later;
- whether update should eventually reject stale `line_id` values that were already superseded, instead of the current active-line lookup behavior.
