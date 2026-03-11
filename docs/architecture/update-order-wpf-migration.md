# Purpose

This document records the first WPF migration step for Orders-tab update/save:

- legacy local update path remains available
- server-centric update path is available under feature flag
- canonical remote write is `PUT /api/orders/{orderId}`

# Canonical WPF update flow

In server mode:

1. `MainWindow` opens `OrderDetailsWindow`
2. `OrderDetailsWindow` collects the editable order snapshot
3. `OrderDetailsWindow` calls `WpfUpdateOrderService`
4. `WpfUpdateOrderService` calls `UpdateOrderApiClient`
5. `UpdateOrderApiClient` sends `PUT /api/orders/{orderId}`
6. WPF accepts server-authoritative `order_ref`
7. WPF reloads the order through the existing read-side flow

Files:

- `apps/windows/FlowStock.App/OrderDetailsWindow.xaml.cs`
- `apps/windows/FlowStock.App/Services/WpfUpdateOrderService.cs`
- `apps/windows/FlowStock.App/Services/UpdateOrderApiClient.cs`

# Feature flag

Saved setting:

```json
{
  "server": {
    "use_server_update_order": true
  }
}
```

Environment override:

- `FLOWSTOCK_USE_SERVER_UPDATE_ORDER=true`

UI:

- `Сервис -> Подключение к БД -> Server API mode -> Use Server API for order updates`

# Legacy vs server behavior

Legacy mode:

- `OrderDetailsWindow` calls `_services.Orders.UpdateOrder(...)`
- local uniqueness check of `order_ref` remains blocking

Server mode:

- `OrderDetailsWindow` calls `WpfUpdateOrderService`
- local uniqueness check of `order_ref` is no longer authoritative
- server may replace requested `order_ref`
- returned `order_id` is used to reload the updated order

# User-visible behavior

In server mode WPF now:

- sends header + full lines snapshot
- shows validation errors returned by server
- shows transport errors for timeout / server unavailable
- shows informational message when server replaces `order_ref`
- reloads the order after successful update

# Manual checklist

1. Legacy update path
   - disable `use_server_update_order`
   - open order
   - change fields/lines
   - save
   - verify old local behavior remains

2. Server update path
   - enable `use_server_update_order`
   - open order
   - change fields/lines
   - save
   - verify changes applied and order was reloaded

3. `order_ref` collision
   - request already used `order_ref`
   - save in server mode
   - verify WPF accepts replacement ref

4. Validation error
   - invalid partner / invalid due date / invalid lines
   - verify WPF shows server-side validation error

5. Timeout / server unavailable
   - stop `FlowStock.Server`
   - save in server mode
   - verify transport error message

6. Legacy rollback
   - disable `use_server_update_order`
   - verify local update path still works

# Remaining gaps before DeleteOrder / SetOrderStatus migration

- `DeleteOrder` is still local
- direct `SetOrderStatus` is still local
- `IncomingRequestsWindow` still uses its existing approval flow
- Orders tab still uses direct DB read-side
