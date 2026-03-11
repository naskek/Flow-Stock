# Purpose

This document records the first WPF migration step for Orders tab create:

- legacy local create path remains available
- server-centric create path is now available under feature flag
- canonical remote write is `POST /api/orders`

# Canonical WPF create flow

In server mode the flow is:

1. `MainWindow` opens `OrderDetailsWindow`
2. `OrderDetailsWindow` collects order header and in-memory lines
3. `OrderDetailsWindow` calls `WpfCreateOrderService`
4. `WpfCreateOrderService` calls `CreateOrderApiClient`
5. `CreateOrderApiClient` sends `POST /api/orders`
6. WPF accepts server-authored `order_id` and `order_ref`
7. WPF reloads the created order through the existing direct DB read path

Files:

- `apps/windows/FlowStock.App/MainWindow.xaml.cs`
- `apps/windows/FlowStock.App/OrderDetailsWindow.xaml.cs`
- `apps/windows/FlowStock.App/Services/WpfCreateOrderService.cs`
- `apps/windows/FlowStock.App/Services/CreateOrderApiClient.cs`

# Feature flag

Saved setting:

```json
{
  "server": {
    "use_server_create_order": true
  }
}
```

Environment override:

- `FLOWSTOCK_USE_SERVER_CREATE_ORDER=true`

UI:

- `Сервис -> Подключение к БД -> Server API mode -> Use Server API for order creation`

# Legacy vs server behavior

Legacy mode:

- `OrderDetailsWindow` calls `_services.Orders.CreateOrder(...)`
- local `order_ref` uniqueness remains blocking
- blank `order_ref` is not allowed

Server mode:

- `OrderDetailsWindow` calls `WpfCreateOrderService`
- local `order_ref` uniqueness is no longer authoritative
- blank `order_ref` is allowed
- server may replace requested `order_ref`
- returned `order_id` is used to reload the created order

# WPF-specific checks that remain client-side

Still client-side:

- dialog field collection
- partner selection UX
- line editing inside the modal before save
- local prefill of next numeric `order_ref` as UX hint

No longer authoritative in server mode:

- local uniqueness of `order_ref`
- local requirement that `order_ref` must be non-empty

# Server-authoritative rules in create mode

Authoritative on server:

- final `order_ref`
- `order_ref_changed`
- persisted `order_id`
- create-time validation
- duplicate line normalization by `item_id`

# Error handling in WPF server mode

Handled explicitly:

- validation error -> warning message from server error mapping
- timeout -> transport timeout message
- server unavailable -> transport/network message
- invalid TLS -> configuration message

No retry/idempotency was added in this slice.

# Manual checklist

1. Legacy create path
   - disable `use_server_create_order`
   - create an order
   - verify existing local behavior remains unchanged

2. Server create path
   - enable `use_server_create_order`
   - create a `CUSTOMER` order
   - verify order is created and reopened through existing read-side flow

3. Missing `order_ref`
   - clear the `Номер` field
   - save in server mode
   - verify WPF accepts server-generated `order_ref`

4. Colliding requested `order_ref`
   - enter an existing order number
   - save in server mode
   - verify WPF accepts replacement `order_ref`

5. Validation error
   - try invalid partner / no lines / invalid qty
   - verify a server-side error is shown and the window stays open

6. Timeout / server unavailable
   - stop `FlowStock.Server`
   - save in server mode
   - verify transport error message is shown

7. Legacy rollback
   - disable `use_server_create_order`
   - verify local create path still works

# Incoming requests convergence

`IncomingRequestsWindow` now has a separate feature-flagged bridge for `CREATE_ORDER` approvals:

- `server.use_server_incoming_request_order_approval`
- bridge path: `IncomingRequestOrderApiBridgeService -> POST /api/orders`

This does not change Orders-tab create path itself; it only removes local direct writes from incoming-request approval when the separate flag is enabled.

# Remaining gaps before removing legacy local create

- `UpdateOrder` is still local
- `DeleteOrder` is still local
- direct `SetOrderStatus` is still local
- `IncomingRequestsWindow` still has legacy fallback when its dedicated flag is off
- Orders-tab create is covered at service/adapter level, not by full UI automation
