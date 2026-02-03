# FlowStock Server MVP Spec

## Scope
- Server-centric workflow. The server is the single source of truth.
- Desktop WPF client works against the server API (online).
- TSD PWA works offline and syncs via JSONL files.
- PostgreSQL is the primary storage. SQLite is dev-only if needed.
- JSON (non-JSONL) exchange is not used.
- Stock is computed from ledger only.

## Components
- FlowStock.Server: ASP.NET Core Minimal API, DB access, diagnostics, JSONL import.
- FlowStock.App: WPF desktop client UI.
- TSD PWA: offline data capture and JSONL export/import.

## Data model (server DB)
- items(id, name, barcode, gtin, uom)
- locations(id, code, name)
- partners(id, name, inn, ... )
- orders(id, order_ref, partner_id, due_date, status, comment, created_at)
- order_lines(id, order_id, item_id, qty_ordered)
- docs(id, doc_ref, type, status, created_at, closed_at, partner_id, order_id, order_ref, shipping_ref)
- doc_lines(id, doc_id, item_id, qty, from_location_id, to_location_id, uom, from_hu, to_hu)
- ledger(id, ts, doc_id, item_id, location_id, qty_delta, hu_code)
- imported_events(event_id, imported_at, source_file, device_id)
- import_errors(id, event_id, reason, raw_json, created_at)

## Invariants
- Stock is derived only from ledger.
- Closed documents are immutable.
- Corrections are separate documents (out of MVP scope).

## JSONL exchange (offline sync)
- JSONL is the only offline format.
- One line = one JSON object.
- Operations import JSONL (events).
- Reference data export uses JSONL (items, locations, partners) when needed.
- JSON (non-JSONL) files are deprecated and not used.

## JSONL event format (operations)
```json
{
  "event_id": "UUID",
  "ts": "YYYY-MM-DDTHH:MM:SS",
  "device_id": "TSD-01",
  "op": "INBOUND|WRITE_OFF|MOVE|INVENTORY|OUTBOUND",
  "doc_ref": "SHIFT-YYYY-MM-DD-OP1-INBOUND",
  "barcode": "string",
  "qty": 10,
  "from": "LOC-CODE-or-null",
  "to": "LOC-CODE-or-null",
  "from_hu": "HU-or-null",
  "to_hu": "HU-or-null"
}
```

## Import rules
- Idempotency: if event_id already in imported_events, skip.
- Unknown barcode: add to import_errors with reason UNKNOWN_BARCODE; do not crash.
- Unknown location: add to import_errors with reason UNKNOWN_LOCATION.
- Invalid JSONL or missing required fields: add to import_errors (INVALID_JSON or MISSING_FIELD).
- Documents are grouped by (doc_ref + op) and created as DRAFT.
- Each event appends a doc_line.
- Ledger entries are created only when the document is closed.

## Ledger rules on close
- INBOUND: +qty to to-location.
- WRITE_OFF: -qty from from-location.
- MOVE: -qty from from-location and +qty to to-location.
- OUTBOUND: -qty from from-location.
- INVENTORY: ledger logic deferred in MVP (document can be closed, but no ledger entries yet).

## UI screens (MVP)
- Status: stock list + search.
- Documents: list with JSONL import panel and access to import errors.
- Document: lines + "Close" action.
- Items: list + create (name, barcode, gtin, uom).
- Locations: list + create (code, name).
- Partners: list + create.
- Orders: list + details (see spec_orders.md).

Import errors are shown in a modal window that lets you bind or create items for UNKNOWN_BARCODE and reapply errors.
