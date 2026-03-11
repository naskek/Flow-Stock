# Purpose

This document defines the canonical server contract for append-only draft line deletion in FlowStock.

Canonical endpoint:

- `POST /api/docs/{docUid}/lines/delete`

This operation is part of draft lifecycle only.

It does not:

- write `ledger`;
- change `docs.status`;
- physically delete historical `doc_lines` rows.

# Canonical operation definition

`DeleteDocLine` is modeled as an append-only tombstone operation.

Server behavior:

1. resolve canonical remote draft identity by `doc_uid`;
2. verify the document exists and is still `DRAFT`;
3. resolve the currently active line by `line_id`;
4. append a tombstone row into `doc_lines`;
5. record one replay/idempotency event in `api_events`;
6. return canonical delete result.

# Append-only delete semantics

Existing active line:

- `doc_lines.id = 101, qty = 10, replaces_line_id = NULL`

Delete request for `line_id = 101` produces:

- original historical row remains unchanged;
- a new tombstone row is appended, for example:
  - `doc_lines.id = 205, qty = 0, replaces_line_id = 101`

Effective read model rule:

- rows superseded by a newer `replaces_line_id` are inactive;
- rows with `qty <= 0` are tombstones and are not returned as active document lines.

# Request DTO

```json
{
  "event_id": "evt-line-delete-001",
  "device_id": "WPF-01",
  "line_id": 101
}
```

Required fields:

- `event_id`
- `line_id`

Optional fields:

- `device_id`

Reason/comment is intentionally not part of v1 delete contract.

# Response DTO

Successful delete:

```json
{
  "ok": true,
  "result": "DELETED",
  "doc_uid": "wpf-doc-42",
  "doc_status": "DRAFT",
  "appended": true,
  "idempotent_replay": false,
  "line": {
    "id": 205,
    "replaces_line_id": 101,
    "item_id": 100,
    "qty": 0,
    "uom_code": "BOX",
    "order_line_id": null,
    "from_location_id": null,
    "to_location_id": 10,
    "from_hu": null,
    "to_hu": null
  }
}
```

Replay response:

```json
{
  "ok": true,
  "result": "IDEMPOTENT_REPLAY",
  "doc_uid": "wpf-doc-42",
  "doc_status": "DRAFT",
  "appended": false,
  "idempotent_replay": true,
  "line": {
    "id": 205,
    "replaces_line_id": 101,
    "item_id": 100,
    "qty": 0,
    "uom_code": "BOX",
    "order_line_id": null,
    "from_location_id": null,
    "to_location_id": 10,
    "from_hu": null,
    "to_hu": null
  }
}
```

# Validation rules

Canonical server validation:

- `doc_uid` must resolve to an existing `api_docs` mapping;
- target `docs` row must exist;
- document must still be `DRAFT`;
- `line_id` must be provided and be positive;
- `line_id` must refer to a currently active line of the target draft.

Canonical failure codes:

- `EMPTY_BODY`
- `INVALID_JSON`
- `MISSING_EVENT_ID`
- `MISSING_LINE_ID`
- `DOC_NOT_FOUND`
- `DOC_NOT_DRAFT`
- `UNKNOWN_LINE`
- `EVENT_ID_CONFLICT`

# Idempotency / replay behavior

Event type:

- `DOC_LINE_DELETE`

Same `event_id` plus same normalized payload:

- returns `IDEMPOTENT_REPLAY`;
- does not append another tombstone row;
- does not record another `DOC_LINE_DELETE` event.

Same `event_id` plus different normalized payload:

- returns `400 BadRequest`;
- returns `ApiResult(false, "EVENT_ID_CONFLICT")`;
- does not change authoritative state.

Different `event_id` plus same `line_id` after the line is already deleted:

- not treated as replay;
- current canonical behavior is validation failure through `UNKNOWN_LINE`, because the original line is no longer active.

# Normalized payload comparison

Delete replay normalization uses:

- `doc_uid`
- `event_id`
- `device_id`
- `line_id`

Normalization rules:

- string fields are trimmed;
- string comparison is case-insensitive;
- `line_id` is compared by parsed numeric value.

# Logging

Structured business logging uses:

- `operation=DeleteDocLine`

Typical success log:

```text
doc_lifecycle operation=DeleteDocLine path=/api/docs/wpf-doc-42/lines/delete result=DELETED doc_uid=wpf-doc-42 doc_id=42 doc_ref=IN-2026-000123 doc_type=INBOUND doc_status_before=DRAFT doc_status_after=DRAFT line_count=0 line_id=205 replaces_line_id=101 ledger_rows_written=0 event_id=evt-line-delete-001 device_id=WPF-01 api_event_written=True appended=True idempotent_replay=False already_closed= elapsed_ms=8 errors=
```

# WPF migration boundary

Manual delete in WPF can use a feature-flagged server adapter:

- `DeleteDocLineApiClient`
- `WpfDeleteDocLineService`
- `server.use_server_delete_doc_line`

Legacy local delete path remains available behind the flag during migration.

Canonical WPF server mode behavior:

- WPF sends one delete request per selected line;
- WPF explicit batch rebuild flows can also reuse the same delete operation to remove current active lines before canonical add-line rebuild;
- WPF outbound HU split / reassignment can also reuse the same delete operation when the source line must disappear entirely before the resulting split line is re-added through canonical add;
- WPF reloads document and lines after success or replay;
- WPF does not fake local delete as authoritative state.

# Tests required

Server integration tests should cover:

1. successful delete appends one tombstone row;
2. original row remains in history;
3. active line projection no longer returns the deleted line;
4. `docs.status` remains `DRAFT`;
5. `ledger` remains untouched;
6. same `event_id` plus same payload returns `IDEMPOTENT_REPLAY`;
7. same `event_id` plus different payload returns `EVENT_ID_CONFLICT`;
8. unknown `line_id` returns validation failure;
9. non-draft document rejects delete.

# Migration notes

This step does not migrate:

- JSONL import;
- server-side reason/comment semantics for delete.

Current WPF explicit batch rebuild now can use canonical delete + canonical add together, but the overall rebuild gesture is still not atomic end-to-end because delete and add remain separate requests.

Outbound HU split / reassignment in full server line lifecycle mode follows the same non-atomic pattern:

- source line is deleted or updated through canonical line APIs;
- resulting split / reassigned line is created through canonical add;
- WPF must refresh authoritative state after partial failure before retrying.
