# Purpose

This document defines the first canonical server-centric `UpdateOrder` slice for Orders tab.

Scope:

- direct canonical update of an existing order
- WPF Orders-tab save/update path

Out of scope in this slice:

- `DeleteOrder`
- direct `SetOrderStatus`
- `IncomingRequestsWindow`
- idempotency / replay

# Current-state assumptions

Current local implementation anchor:

- `apps/windows/FlowStock.Core/Services/OrderService.cs`
- `UpdateOrder(...)`

Current WPF save/update entry point:

- `apps/windows/FlowStock.App/OrderDetailsWindow.xaml.cs`

Read-side remains unchanged:

- WPF continues to reload updated orders through the current DB-backed read path

# Canonical operation definition

Canonical operation:

- update an existing order by replacing the mutable order snapshot on the server

First-slice semantics:

- update mutable header fields
- replace the persisted line snapshot from the request payload
- normalize duplicate item rows server-side
- do not write `docs`
- do not write `ledger`

# Proposed endpoint

- `PUT /api/orders/{orderId}`

`orderId` is the authoritative server identity for update.

# Request DTO

```json
{
  "order_ref": "optional requested ref",
  "type": "CUSTOMER|INTERNAL",
  "partner_id": 123,
  "due_date": "yyyy-MM-dd",
  "status": "DRAFT|ACCEPTED|IN_PROGRESS",
  "comment": "optional comment",
  "lines": [
    {
      "item_id": 1001,
      "qty_ordered": 12.0
    }
  ]
}
```

Notes:

- `type` is required for validation but is not mutable
- `lines` are treated as a full replacement snapshot

# Response DTO

```json
{
  "ok": true,
  "result": "UPDATED",
  "order_id": 501,
  "order_ref": "004",
  "order_ref_changed": false,
  "type": "CUSTOMER",
  "status": "IN_PROGRESS",
  "line_count": 2
}
```

# Mutable fields

Mutable in this first slice:

- `order_ref`
- `partner_id`
- `due_date`
- `status`
- `comment`
- full `lines` snapshot

Not mutable in this slice:

- `order_id`
- `type`

# order_ref behavior

Server-authoritative behavior:

1. requested non-empty free `order_ref`
   - accepted as-is
   - `order_ref_changed = false`

2. requested non-empty colliding `order_ref`
   - replaced by server with next generated numeric ref
   - `order_ref_changed = true`

3. blank or missing `order_ref`
   - server preserves the existing order ref
   - `order_ref_changed = false`

# Line replacement semantics

First-slice line semantics:

- request carries the full desired line snapshot
- server validates all incoming lines
- duplicate `item_id` rows are normalized
- persisted result contains one line per item
- lines absent from the incoming snapshot are removed
- lines present in the incoming snapshot are added or updated as needed

Implementation anchor:

- existing `OrderService.UpdateOrder(...)`

# Validation rules

Canonical validation in this slice:

- `orderId` must exist
- existing order must not be `SHIPPED`
- request body must be valid JSON
- `type` must be valid and must match existing order type
- requested `status` must be valid
- `SHIPPED` status is forbidden on update
- for `CUSTOMER`, partner is required
- partner must exist
- supplier partner is forbidden
- `due_date` must be valid `yyyy-MM-dd` when provided
- `lines` must contain at least one row
- every line must have valid `item_id`
- every line must have `qty_ordered > 0`

# Guarantees

Guaranteed in this slice:

- no `docs` writes
- no `ledger` writes
- no convergence with `order_requests`
- no idempotency or replay semantics

# Compatibility with WPF

WPF migration shape:

- feature flag: `server.use_server_update_order`
- env override: `FLOWSTOCK_USE_SERVER_UPDATE_ORDER=true`
- `OrderDetailsWindow` collects the full order snapshot
- WPF sends that snapshot to `PUT /api/orders/{orderId}`
- WPF accepts server-replaced `order_ref`
- WPF reloads the order after successful update

Legacy local update remains available when the feature flag is off.

# Out of scope

Still out of scope after this slice:

- `DeleteOrder`
- direct canonical `SetOrderStatus`
- `IncomingRequestsWindow` convergence
- order-create idempotency
- order-update idempotency
- read-side refactor for Orders tab

# Test specification

Minimum integration coverage:

- successful update of existing order
- response returns `order_id/order_ref/status`
- header update works
- line snapshot replacement works
- duplicate item lines normalize
- missing lines fails
- invalid partner fails
- supplier partner fails
- invalid due date fails
- `SHIPPED` status forbidden
- existing shipped order not editable
- colliding requested `order_ref` replaced
- no `docs` writes
- no `ledger` writes
- unknown `orderId` fails

WPF compatibility coverage:

- feature flag routes update to canonical endpoint
- WPF accepts server-replaced `order_ref`
- legacy local update remains available when flag is off

# Decisions requiring confirmation

- whether blank `order_ref` on update should keep existing value permanently, or later become a hard validation error
- whether `IN_PROGRESS` should remain directly settable on update, or later become server-derived only
- whether a later slice should move line replacement away from the current local `OrderService.UpdateOrder(...)` implementation anchor
