# Purpose

This document records the convergence of `IncomingRequestsWindow` order approval actions from legacy local writes to canonical order API paths.

Scope of this slice:

- keep existing incoming-request read workflow
- keep existing reject workflow
- route approval writes through canonical order API under feature flag

Out of scope:

- orders read-side migration
- new approval workflow
- idempotency/replay for orders

# Current-state approval flow

Before this convergence:

- `IncomingRequestsWindow` loaded requests via `IDataStore.GetOrderRequests(...)`
- `CREATE_ORDER` approval called local `_services.Orders.CreateOrder(...)`
- `SET_ORDER_STATUS` approval called local `_services.Orders.ChangeOrderStatus(...)`
- after local write, WPF called `ResolveOrderRequest(...)`

Files:

- `apps/windows/FlowStock.App/IncomingRequestsWindow.xaml.cs`
- `apps/windows/FlowStock.Core/Services/OrderService.cs`
- `apps/windows/FlowStock.Data/PostgresDataStore.cs`

# Canonical target flow

Canonical approval flow under feature flag:

1. `IncomingRequestsWindow`
2. `IncomingRequestOrderApiBridgeService`
3. canonical order API client
4. canonical order endpoint
5. only after canonical success: `ResolveOrderRequest(...)`

Legacy fallback remains available when the feature flag is off.

# CREATE_ORDER request -> POST /api/orders mapping

`CREATE_ORDER` request payload is transformed to canonical create-order request:

- `order_ref` -> `order_ref`
- `partner_id` -> `partner_id`
- `due_date` -> `due_date`
- `comment` -> `comment`
- `lines[].item_id` -> `lines[].item_id`
- `lines[].qty_ordered` -> `lines[].qty_ordered`

Canonical values added by the bridge:

- `type = CUSTOMER`
- `status = ACCEPTED`

Target endpoint:

- `POST /api/orders`

# SET_ORDER_STATUS request -> POST /api/orders/{orderId}/status mapping

`SET_ORDER_STATUS` request payload is transformed to canonical status request:

- `order_id` -> route parameter `{orderId}`
- `status` -> request body `status`

Target endpoint:

- `POST /api/orders/{orderId}/status`

# Request status handling after canonical success/failure

On canonical success:

- request is marked `APPROVED`
- `resolved_by` is set from current WPF operator
- `resolution_note` reflects canonical action result
- `applied_order_id` is set for `CREATE_ORDER`
- `applied_order_id` is preserved as target order id for `SET_ORDER_STATUS`

On canonical validation failure:

- request remains `PENDING`
- request is not incorrectly marked approved

On transport failure / timeout:

- request remains `PENDING`
- operator sees a transport error and can retry later

# Validation/error handling

Validation stays authoritative on canonical endpoints:

- `CREATE_ORDER` validation comes from `POST /api/orders`
- `SET_ORDER_STATUS` validation comes from `POST /api/orders/{orderId}/status`

Bridge-level behavior:

- payload parse failure -> approval fails locally, request stays `PENDING`
- canonical validation failure -> approval fails, request stays `PENDING`
- timeout / server unavailable / TLS failure -> approval fails, request stays `PENDING`

# Compatibility with current IncomingRequestsWindow UX

What stays the same:

- user still reviews pending requests in `IncomingRequestsWindow`
- approve/reject buttons remain in the same place
- request list reloads after approval attempt

What changes in server mode:

- approve no longer writes orders locally through `OrderService`
- success depends on canonical API result
- failure message may now come from canonical create/status validation

# Out of scope

- reject flow
- `IncomingRequestsWindow` read-side
- create/update/delete/status migration for orders tab itself
- request intake endpoints `/api/orders/requests/create` and `/api/orders/requests/status`

# Test specification

Adapter/service-level tests cover:

1. approve `CREATE_ORDER` -> routes to canonical `POST /api/orders`
2. approve `SET_ORDER_STATUS` -> routes to canonical `POST /api/orders/{orderId}/status`
3. canonical success marks request as approved
4. canonical validation failure does not mark request approved
5. legacy fallback remains available when feature flag is off

# Decisions requiring confirmation

- none for this slice; convergence reuses already accepted canonical order endpoints

# Manual checklist

1. Legacy approval path
   - disable `use_server_incoming_request_order_approval`
   - approve `CREATE_ORDER`
   - approve `SET_ORDER_STATUS`
   - verify old behavior still works

2. Server approval path
   - enable `use_server_incoming_request_order_approval`
   - approve `CREATE_ORDER`
   - verify canonical `POST /api/orders` is used and request becomes approved

3. Approve `SET_ORDER_STATUS`
   - verify canonical `POST /api/orders/{orderId}/status` is used
   - verify request becomes approved only on canonical success

4. Validation failure
   - use invalid request payload or invalid target state
   - verify request stays pending

5. Timeout / server unavailable
   - stop `FlowStock.Server`
   - verify transport error is shown
   - verify request status stays consistent
