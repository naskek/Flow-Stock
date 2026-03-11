# Purpose

This document describes the current WPF migration state for `AddDocLine` after enabling server-centric API usage for:

- manual line add (`+ Товар`)
- batch line creation from order-driven flows

Canonical server path remains:

- `POST /api/docs/{docUid}/lines`

Server semantics remain unchanged:

- append-only
- idempotency by `event_id`
- no ledger writes
- no `docs.status` transition

# Scope of migrated WPF flows

Migrated to canonical server add-line under feature flags:

- manual WPF line add from `OperationDetailsWindow`
- outbound order fill / refill flow previously backed by `ApplyOrderToDoc()`
- production receipt fill-from-order flow previously backed by `ApplyOrderToProductionReceipt()`
- outbound order re-apply from `TrySaveHeaderAsync()` when order-bound lines must be rebuilt
- explicit batch rebuild delete phase for outbound and production receipt, using canonical `DeleteDocLine`
- outbound HU allocation from `OutboundHuApply_Click()` when full server line lifecycle mode is enabled
- outbound HU reassignment / split from `AssignHuButton_Click()` when full server line lifecycle mode is enabled

Still legacy-local in this step:

- non-outbound HU assignment / distribution flows
- JSONL import
- any batch rebuild flow when full server rebuild mode is not enabled

Important boundary:

- batch line creation uses API calls per line;
- in full server rebuild mode, old active lines are removed through canonical delete requests instead of local `DeleteDocLines()`;
- non-destructive header/order-binding writes remain local for now.

# Files involved

WPF entry points:

- `apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs`
  - `DocAddLine_Click()`
  - `TryApplyOrderSelectionAsync()`
  - `TryApplyOrderSelectionViaServerAsync()`
  - `TryApplyReceiptOrderSelectionAsync()`
  - `FillProductionReceiptFromOrderAsync()`
  - `TrySaveHeaderAsync()`

WPF API orchestration:

- `apps/windows/FlowStock.App/Services/WpfAddDocLineService.cs`
  - `AddLineAsync()` for manual add
  - shared temporary `api_docs` / `doc_uid` mapping bridge
- `apps/windows/FlowStock.App/Services/WpfBatchAddDocLineService.cs`
  - `AddLinesBatchAsync()` for batch flows
  - one HTTP `POST /api/docs/{docUid}/lines` per batch line
  - shared temporary `api_docs` / `doc_uid` mapping bridge
- `apps/windows/FlowStock.App/Services/WpfUpdateDocLineService.cs`
  - `UpdateLineAsync()` for manual quantity edits of existing lines
  - feature-flagged WPF bridge to canonical append-only update endpoint
- `apps/windows/FlowStock.App/Services/WpfDeleteDocLineService.cs`
  - `DeleteLinesAsync()` for manual deletion of selected lines
  - feature-flagged WPF bridge to canonical append-only delete endpoint

WPF HTTP client:

- `apps/windows/FlowStock.App/Services/AddDocLineApiClient.cs`
- `apps/windows/FlowStock.App/Services/UpdateDocLineApiClient.cs`
- `apps/windows/FlowStock.App/Services/DeleteDocLineApiClient.cs`

WPF settings:

- `apps/windows/FlowStock.App/Services/SettingsService.cs`
  - `server.use_server_add_doc_line`
  - `server.use_server_update_doc_line`
  - `server.use_server_delete_doc_line`

WPF settings UI:

- `apps/windows/FlowStock.App/DbConnectionWindow.xaml`
- `apps/windows/FlowStock.App/DbConnectionWindow.xaml.cs`

Legacy local services still used in hybrid mode:

- `apps/windows/FlowStock.Core/Services/DocumentService.cs`
  - `ApplyOrderToDoc()`
  - `ApplyOrderToProductionReceipt()`
  - `UpdateDocOrderBinding()`
- `apps/windows/FlowStock.Data/PostgresDataStore.cs`
  - `DeleteDocLines()`

# Feature flags

Saved in `%APPDATA%\\FlowStock\\settings.json`:

```json
{
  "server": {
    "use_server_add_doc_line": true,
    "base_url": "https://127.0.0.1:7154",
    "device_id": "WPF-MACHINE",
    "close_timeout_seconds": 15,
    "allow_invalid_tls": false
  }
}
```

Environment overrides:

- `FLOWSTOCK_USE_SERVER_ADD_DOC_LINE=true`
- `FLOWSTOCK_USE_SERVER_UPDATE_DOC_LINE=true`
- `FLOWSTOCK_USE_SERVER_DELETE_DOC_LINE=true`

UI path:

- `Сервис -> Подключение к БД`
- `Use Server API for manual '+ Товар'`
- `Use Server API for quantity edits of existing lines`
- `Use Server API for deleting existing lines`

Full server batch rebuild mode requires both:

- `use_server_add_doc_line`
- `use_server_delete_doc_line`

There is no separate batch toggle. If delete-line server mode is disabled, batch rebuild falls back to legacy local path instead of mixing local destructive delete with server add-line.

# WPF behavior in batch server mode

# WPF line update mode

Manual quantity edits from `Изменить количество...` now support a separate server mode:

1. WPF reads the currently active line from the document.
2. WPF keeps dialog-side quantity/UOM selection and existing line location/HU as client-side preparation.
3. WPF sends `POST /api/docs/{docUid}/lines/update` through `WpfUpdateDocLineService`.
4. Server appends a replacement line with `replaces_line_id = old_line_id`.
5. WPF reloads the document and line grid from DB after success or replay.

Important boundary:

- WPF does not try to merge or mutate the old row locally;
- server remains authoritative for append-only update semantics;
- delete of selected lines can also use server append-only tombstones under feature flag;
- outbound HU split/reassignment can now reuse canonical update/delete + add in full server mode.

# WPF line delete mode

Manual delete of selected lines now supports a separate server mode:

1. WPF resolves selected active line ids from the grid.
2. WPF sends one `POST /api/docs/{docUid}/lines/delete` request per selected line through `WpfDeleteDocLineService`.
3. Server appends one tombstone row per accepted delete and records one `DOC_LINE_DELETE` event.
4. WPF reloads the document and line grid after success or replay.

Important boundary:

- manual delete and explicit batch rebuild delete phases are migrated in this step;
- destructive batch pre-steps that clear lines before rebuild are no longer local in full server rebuild mode;
- WPF does not hide or fake server delete outcome and always refreshes authoritative state from DB.

## Batch line flows (ApplyOrderToDoc, ApplyOrderToProductionReceipt)

## Outbound order fill

When batch server mode is enabled and WPF needs to rebuild outbound lines from an order:

1. WPF validates current location/HU UI state.
2. WPF builds intended line contexts from `GetOrderShipmentRemaining(orderId)`.
3. WPF still performs local non-destructive pre-steps:
   - update header/order binding
4. WPF resolves current active draft lines and removes them through `WpfDeleteDocLineService`.
5. WPF then sends one `POST /api/docs/{docUid}/lines` request per line via `WpfBatchAddDocLineService.AddLinesBatchAsync()`.
5. WPF reloads the document and line grid after the batch attempt.

## Production receipt fill from order

When batch server mode is enabled for production receipt fill:

1. WPF validates receipt location/HU UI state.
2. WPF builds intended line contexts from `GetOrderReceiptRemaining(orderId)`.
3. WPF still performs local non-destructive pre-steps:
   - update order binding
4. If the user chose replace, WPF removes existing active draft lines through `WpfDeleteDocLineService`.
5. WPF sends one canonical add-line request per line.
5. WPF reloads the document and line grid after the batch attempt.

# Client-side vs server-side responsibility

Client-side in WPF:

- item/order selection UX
- quantity and location/HU UI validation before call
- deciding whether a batch should rebuild lines
- local pre-steps for not-yet-migrated non-destructive operations
- showing timeout / validation / partial-failure messages

Authoritative on server:

- append-only add-line write
- line validation
- replay / idempotency
- `same event_id + same payload -> IDEMPOTENT_REPLAY`
- `same event_id + different payload -> EVENT_ID_CONFLICT`

# Temporary hybrid boundary

This step is intentionally hybrid.

Why:

- WPF batch flows historically combine several actions in one UX gesture:
  - order binding update
  - line replacement
  - line creation
- canonical line creation, line update and line delete are already available through server APIs
- explicit batch rebuild and outbound HU split / reassignment now reuse those canonical line APIs
- non-destructive header/order-binding writes are still local

Current consequence:

- a batch rebuild is not yet atomic end-to-end
- canonical delete may succeed before all canonical add-line requests complete
- after timeout or mid-batch failure, WPF must reload the document and the user must inspect the resulting line set before retrying
- outbound HU split / reassignment is also non-atomic end-to-end in full server mode because source update/delete and resulting add remain separate requests

# WPF outbound HU split / reassignment mode

When full server line lifecycle mode is enabled for `Outbound`, WPF no longer uses local `DeleteDocLine()`, `UpdateDocLineQty()` and `AddDocLine()` for these flows:

- `OutboundHuApply_Click()` from the outbound HU stock panel
- `AssignHuButton_Click()` for outbound line reassignment to a selected HU

Required flags:

- `use_server_add_doc_line`
- `use_server_update_doc_line`
- `use_server_delete_doc_line`

Server-mode sequence:

1. WPF resolves the active source line and the intended target HU/location.
2. If the source line must disappear entirely, WPF calls canonical `DeleteDocLine`.
3. If the source line must shrink, WPF calls canonical `UpdateDocLine`.
4. WPF creates the resulting split / reassigned line via canonical `AddDocLine`.
5. WPF reloads the document and line grid from DB.

Important boundary:

- WPF does not mix local destructive writes with server writes inside full server HU mode;
- server remains append-only and authoritative for line history;
- if only a subset of line flags is enabled, outbound HU flows fall back to legacy local behavior and log the reason.

# Manual checklist

## Legacy batch path

1. Disable `use_server_add_doc_line`.
2. Open outbound or production receipt draft.
3. Run `Заполнить из заказа`.
4. Verify old local behavior still works.

## Server batch path: outbound

1. Enable `use_server_add_doc_line`.
2. Enable `use_server_delete_doc_line`.
3. Open outbound draft.
4. Select source location and optional HU.
5. Choose order.
6. Verify old lines are removed through canonical delete and new lines are added through canonical add.
7. Verify lines are rebuilt and reloaded from DB.
8. Verify document stays `DRAFT`.
9. Verify ledger is still untouched until close.

## Server batch path: production receipt

1. Enable `use_server_add_doc_line`.
2. Enable `use_server_delete_doc_line`.
3. Open production receipt draft.
4. Select receipt location.
5. Run `Заполнить из заказа`.
6. Verify lines are created through server path and reloaded from DB.
7. Verify document stays `DRAFT`.

## Large order

1. Enable `use_server_add_doc_line`.
2. Enable `use_server_delete_doc_line`.
3. Open a draft tied to an order with 20+ remaining lines.
4. Run `Заполнить из заказа`.
5. Verify all expected lines appear after refresh.
6. Verify the UI remains responsive while requests are processed.

## Replace existing lines

1. In production receipt server batch mode, create some lines first.
2. Run `Заполнить из заказа` again and choose replace.
3. Verify WPF removes old active lines through canonical delete requests, then recreates target lines through API.
4. Verify final visible state matches refreshed DB state.

## Same semantic line twice

1. Use server batch mode on a scenario that produces the same semantic line twice through two separate user actions.
2. Verify final persisted state is append-only.
3. Do not expect local merge semantics on server-created rows.

## Validation fail

1. Trigger batch add with missing required location/HU context.
2. Verify WPF shows validation failure.
3. Verify document is reloaded after the attempt.

## Timeout / server unavailable

1. Enable `use_server_add_doc_line`.
2. Enable `use_server_delete_doc_line`.
3. Stop `FlowStock.Server` or force timeout.
4. Run batch fill.
5. Verify WPF shows transport/timeout error.
6. Verify WPF reloads document and lines after the attempt.
7. Verify the operator is expected to inspect the document before retrying.

## Idempotency after partial success

1. Enable `use_server_add_doc_line`.
2. Enable `use_server_delete_doc_line`.
3. Start a batch fill and interrupt the server after some delete/add requests are accepted.
4. Restart the server and repeat the batch action.
5. Refresh the document after each attempt.
6. Verify the final visible state reflects only server-accepted delete/add events.

## Outbound HU split / reassignment: server mode

1. Enable:
   - `use_server_add_doc_line`
   - `use_server_update_doc_line`
   - `use_server_delete_doc_line`
2. Open outbound draft.
3. Run outbound HU allocation or outbound `Назначить HU...`.
4. Verify:
   - lines are rebuilt through canonical server requests;
   - document stays `DRAFT`;
   - `ledger` is untouched.
5. Check server log for `UpdateDocLine`, `DeleteDocLine` and `AddDocLine`.
6. Check WPF `app.log` for `wpf_hu_server_flow`.

## Outbound HU split / reassignment: partial failure

1. Enable all three line flags.
2. Start outbound HU split / reassignment.
3. Simulate timeout / server unavailable between source mutation and resulting add.
4. Verify WPF shows a partial-failure message and reloads the document.
5. Verify the user can inspect authoritative lines before deciding on retry.

## End-to-end with canonical lifecycle

1. Enable:
   - `use_server_create_doc_draft`
   - `use_server_add_doc_line`
   - `use_server_delete_doc_line`
   - `use_server_close_document`
2. Create draft in WPF.
3. Fill lines from order through batch mode.
4. Optionally rebuild lines from order.
5. Optionally add one manual line.
6. Optionally delete one line.
7. Close document through server close.
8. Verify lifecycle works without switching back to legacy create/add/delete/close for those steps.

# Remaining gaps before removing legacy WPF AddLine path

- batch rebuilds are not atomic because canonical delete and canonical add still execute as separate requests
- JSONL import stays outside canonical add-line lifecycle
- non-outbound HU reassignment/distribution flows are still local
- timeout recovery is safe but conservative: user must inspect refreshed lines before retrying a failed batch
