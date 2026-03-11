# Purpose

This document records the minimal production implementation choices made for the first Orders-tab migration slice:

- canonical direct `CreateOrder`
- `POST /api/orders`

It complements:

- `docs/architecture/create-order-server-contract.md`
- `docs/architecture/create-order-test-matrix.md`
- `docs/architecture/create-order-test-spec.md`

# Implemented production shape

Implemented endpoint:

- `POST /api/orders`
- file: `apps/windows/FlowStock.Server/OrderCreateEndpoint.cs`

Routing:

- mapped from `apps/windows/FlowStock.Server/Program.cs`
- mapped in HTTP test host through `CloseDocumentHttpHost`

Request DTO:

- `CreateOrderRequest`
- `CreateOrderLineRequest`
- file: `apps/windows/FlowStock.Server/ApiModels.cs`

Response DTO:

- `CreateOrderEnvelope`
- file: `apps/windows/FlowStock.Server/ApiModels.cs`

# Exact POST /api/orders contract

Request shape:

```json
{
  "order_ref": "optional-requested-ref",
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

Response shape on success:

```json
{
  "ok": true,
  "result": "CREATED",
  "order_id": 501,
  "order_ref": "004",
  "order_ref_changed": false,
  "type": "CUSTOMER",
  "status": "DRAFT",
  "line_count": 1
}
```

Validation failure shape:

```json
{
  "ok": false,
  "error": "ERROR_CODE"
}
```

# order_ref generation / collision semantics

Implemented behavior:

1. missing `order_ref`
   - server generates the next numeric order ref using current `GetOrders()` state
   - `order_ref_changed = false`

2. requested free `order_ref`
   - server accepts it as authoritative
   - `order_ref_changed = false`

3. colliding requested `order_ref`
   - server replaces it with the next generated numeric order ref
   - `order_ref_changed = true`

This matches the chosen server-authoritative direction while keeping the implementation minimal.

# Initial status semantics

Implemented create-time statuses:

- `DRAFT`
- `ACCEPTED`
- `IN_PROGRESS`

Default:

- missing `status` -> `DRAFT`

Rejected:

- `SHIPPED`

No auto-status redesign was introduced in this slice.
Current read-time auto-status behavior elsewhere in the module remains unchanged.

# Line normalization semantics

Implemented create-time line behavior:

- request must contain at least one line
- every line must have valid `item_id`
- every line must have `qty_ordered > 0`
- duplicate `item_id` rows in request are accepted
- duplicates are normalized server-side through existing `OrderService.CreateOrder(...)` behavior
- persisted result contains one `order_lines` row per item with summed quantity

# Validation behavior implemented now

Implemented server-side validation codes:

- `EMPTY_BODY`
- `INVALID_JSON`
- `INVALID_TYPE`
- `INVALID_STATUS`
- `SHIPPED_STATUS_FORBIDDEN`
- `MISSING_PARTNER_ID`
- `PARTNER_NOT_FOUND`
- `PARTNER_IS_SUPPLIER`
- `INVALID_DUE_DATE`
- `MISSING_LINES`
- `MISSING_ITEM_ID`
- `INVALID_QTY_ORDERED`
- `ITEM_NOT_FOUND`
- `ORDER_CREATE_FAILED` (defensive internal fallback)

# Implementation anchors

The implementation intentionally reuses the current business create primitive:

- `apps/windows/FlowStock.Core/Services/OrderService.cs`
- `CreateOrder(...)`

Why this was kept:

- minimal diff
- preserves current order-line normalization behavior
- avoids introducing a second competing order-create business path

# What remains deferred

Still deferred for later slices:

- `IncomingRequestsWindow` approval convergence
- `UpdateOrder`
- `DeleteOrder`
- direct canonical `SetOrderStatus`
- idempotency / replay for direct create
- broader refactor of order status ownership

# WPF feature-flagged create bridge

Implemented WPF bridge:

- `apps/windows/FlowStock.App/Services/CreateOrderApiClient.cs`
- `apps/windows/FlowStock.App/Services/WpfCreateOrderService.cs`
- `apps/windows/FlowStock.App/OrderDetailsWindow.xaml.cs`

Feature flag:

- settings: `server.use_server_create_order`
- env override: `FLOWSTOCK_USE_SERVER_CREATE_ORDER`

Behavior in server mode:

- `OrderDetailsWindow` collects header + lines and calls `POST /api/orders`
- local `order_ref` uniqueness no longer blocks create in server mode
- blank `order_ref` is allowed in server mode
- returned `order_id` becomes the local read-side identity used to reload the created order
- returned server-authoritative `order_ref` is accepted as-is
- legacy local create path remains available when the feature flag is off

Behavior intentionally unchanged:

- `UpdateOrder` stays local
- `DeleteOrder` stays local
- manual status changes stay local
- `IncomingRequestsWindow` stays on its existing workflow

# Testing outcome after this step

Server-side and WPF-adapter `CreateOrder` suite status after implementation:

- `17` passing
- `0` skipped
- `0` failed
