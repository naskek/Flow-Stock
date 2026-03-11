# Purpose

Document the current-state architecture of the Orders tab in FlowStock before any server-centric migration work. The goal is to identify where order behavior lives today, which flows already ride on canonical document APIs, which flows still depend on local WPF writes, and which single migration slice has the best value-to-risk ratio.

## Current WPF flow

### Orders tab entry points
- [MainWindow.xaml](d:/FlowStock/apps/windows/FlowStock.App/MainWindow.xaml) hosts the `Заказы` tab and the orders grid.
- [MainWindow.xaml.cs:486](d:/FlowStock/apps/windows/FlowStock.App/MainWindow.xaml.cs:486) `LoadOrders()` loads orders through `_services.Orders.GetOrders()`.
- [MainWindow.xaml.cs:668](d:/FlowStock/apps/windows/FlowStock.App/MainWindow.xaml.cs:668) `OrdersNew_Click()` opens [OrderDetailsWindow.xaml](d:/FlowStock/apps/windows/FlowStock.App/OrderDetailsWindow.xaml) for create.
- [MainWindow.xaml.cs:730](d:/FlowStock/apps/windows/FlowStock.App/MainWindow.xaml.cs:730) `OpenSelectedOrder()` opens the same window for edit.
- [MainWindow.xaml.cs:676](d:/FlowStock/apps/windows/FlowStock.App/MainWindow.xaml.cs:676) `OrdersDelete_Click()` deletes the selected order through `_services.Orders.DeleteOrder(order.Id)`.
- [AppServices.cs:14](d:/FlowStock/apps/windows/FlowStock.App/AppServices.cs:14) and [AppServices.cs:58](d:/FlowStock/apps/windows/FlowStock.App/AppServices.cs:58) wire WPF directly to `OrderService` over the local `IDataStore` / `PostgresDataStore` path.

### Order create/edit window
- [OrderDetailsWindow.xaml](d:/FlowStock/apps/windows/FlowStock.App/OrderDetailsWindow.xaml) provides only order header and order line editing UI. A direct action like "Create outbound from order" was not found in this window.
- [OrderDetailsWindow.xaml.cs:154](d:/FlowStock/apps/windows/FlowStock.App/OrderDetailsWindow.xaml.cs:154) `TrySaveOrder()` is the authoritative WPF save path:
  - create -> `_services.Orders.CreateOrder(...)`
  - edit -> `_services.Orders.UpdateOrder(...)`
- [OrderDetailsWindow.xaml.cs:197](d:/FlowStock/apps/windows/FlowStock.App/OrderDetailsWindow.xaml.cs:197) `AddLine_Click()` and [OrderDetailsWindow.xaml.cs:213](d:/FlowStock/apps/windows/FlowStock.App/OrderDetailsWindow.xaml.cs:213) `AddOrderLine(...)` mutate the in-memory `_lines` collection only until save.
- [OrderDetailsWindow.xaml.cs:308](d:/FlowStock/apps/windows/FlowStock.App/OrderDetailsWindow.xaml.cs:308) `RefreshLineMetrics()` computes UI-only metrics for availability / shipped / produced / remaining.
- [OrderDetailsWindow.xaml.cs:360](d:/FlowStock/apps/windows/FlowStock.App/OrderDetailsWindow.xaml.cs:360) `EnsureEditable()` blocks edits for shipped orders.
- [OrderDetailsWindow.xaml.cs:592](d:/FlowStock/apps/windows/FlowStock.App/OrderDetailsWindow.xaml.cs:592) `GenerateNextOrderRef()` generates the next order ref locally from the current WPF-visible orders list.

### Order-bound outbound / production flows in WPF
Orders influence document flows through [OperationDetailsWindow.xaml.cs](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs), not through the Orders tab itself.

- [OperationDetailsWindow.xaml.cs:2146](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2146) `TryApplyOrderSelectionAsync(...)` is the explicit fill-from-order entry point for `Outbound`.
- [OperationDetailsWindow.xaml.cs:2176](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2176) `TryApplyOrderSelectionLegacy(...)` calls `_services.Documents.ApplyOrderToDoc(...)` and is fully local.
- [OperationDetailsWindow.xaml.cs:2332](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2332) `TryApplyOrderSelectionViaServerAsync(...)` uses the canonical document API path for line lifecycle in server mode:
  - local header/order binding update
  - canonical delete existing doc lines
  - canonical batch add new doc lines
  - reload document and lines
- [OperationDetailsWindow.xaml.cs:2434](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2434) `DocFillFromOrder_Click(...)` is the explicit button for `Outbound` and `ProductionReceipt` fill.
- [OperationDetailsWindow.xaml.cs:2481](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2481) `FillProductionReceiptFromOrderAsync(...)` is the parallel internal-order / production-receipt flow.
- [OperationDetailsWindow.xaml.cs:2856](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2856) `LoadOrderQuantities(...)` loads remaining shipment quantities for partial shipment behavior.
- [OperationDetailsWindow.xaml.cs:2141](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2141) `HasOrderBinding()` identifies the order-bound outbound scenario.
- [OperationDetailsWindow.xaml.cs:1698](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:1698) `DocPartialCheck_Changed(...)` participates in partial shipment UX.

## Current Server/API flow

### Read-side order API
Server-side read endpoints exist in [Program.cs](d:/FlowStock/apps/windows/FlowStock.Server/Program.cs):
- [Program.cs:490](d:/FlowStock/apps/windows/FlowStock.Server/Program.cs:490) `GET /api/orders`
- [Program.cs:520](d:/FlowStock/apps/windows/FlowStock.Server/Program.cs:520) `GET /api/orders/next-ref`
- [Program.cs:528](d:/FlowStock/apps/windows/FlowStock.Server/Program.cs:528) `GET /api/orders/{orderId}`
- [Program.cs:540](d:/FlowStock/apps/windows/FlowStock.Server/Program.cs:540) `GET /api/orders/{orderId}/lines`

These endpoints use `OrderService` on the server side for read-only behavior, including computed status and line metrics.

### Write-side order API
A canonical server write API for direct order create/edit/delete was not found.

What exists instead:
- [Program.cs:568](d:/FlowStock/apps/windows/FlowStock.Server/Program.cs:568) `POST /api/orders/requests/create`
- [Program.cs:697](d:/FlowStock/apps/windows/FlowStock.Server/Program.cs:697) `POST /api/orders/requests/status`

These endpoints do not create or update orders directly. They insert pending `order_requests` for later WPF approval.

### WPF approval of order requests
- [IncomingRequestsWindow.xaml.cs:260](d:/FlowStock/apps/windows/FlowStock.App/IncomingRequestsWindow.xaml.cs:260) `ApplyOrderRequest(...)` is the approval-side write path.
- For `CREATE_ORDER`, [IncomingRequestsWindow.xaml.cs:290](d:/FlowStock/apps/windows/FlowStock.App/IncomingRequestsWindow.xaml.cs:290) calls `_services.Orders.CreateOrder(...)`.
- For `SET_ORDER_STATUS`, [IncomingRequestsWindow.xaml.cs:311](d:/FlowStock/apps/windows/FlowStock.App/IncomingRequestsWindow.xaml.cs:311) calls `_services.Orders.ChangeOrderStatus(...)`.

So even the "server/API" order write flow still resolves into local WPF business writes.

## Current DB interactions

### Orders tab local writes
[OrderService.cs](d:/FlowStock/apps/windows/FlowStock.Core/Services/OrderService.cs) is the central write service.

- [OrderService.cs:58](d:/FlowStock/apps/windows/FlowStock.Core/Services/OrderService.cs:58) `CreateOrder(...)`
  - validates header
  - normalizes lines
  - transaction: `AddOrder()` + `AddOrderLine()`
- [OrderService.cs:114](d:/FlowStock/apps/windows/FlowStock.Core/Services/OrderService.cs:114) `UpdateOrder(...)`
  - validates order immutability rules
  - transaction: `UpdateOrder()` + line-by-line `UpdateOrderLineQty()` / `AddOrderLine()` / `DeleteOrderLine()`
  - also performs legacy duplicate cleanup by `item_id`
- [OrderService.cs:215](d:/FlowStock/apps/windows/FlowStock.Core/Services/OrderService.cs:215) `DeleteOrder(...)`
  - transaction: `DeleteOrderLines(orderId)` + `DeleteOrder(orderId)`
- [OrderService.cs:257](d:/FlowStock/apps/windows/FlowStock.Core/Services/OrderService.cs:257) `ChangeOrderStatus(...)`
  - direct `UpdateOrderStatus(orderId, status)`

### PostgresDataStore methods used
Relevant SQL/write methods in [PostgresDataStore.cs](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs):
- [PostgresDataStore.cs:1467](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1467) `AddOrder(...)`
- [PostgresDataStore.cs:1487](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1487) `UpdateOrder(...)`
- [PostgresDataStore.cs:1513](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1513) `UpdateOrderStatus(...)`
- [PostgresDataStore.cs:1525](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1525) `GetOrderLines(...)`
- [PostgresDataStore.cs:1542](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1542) `GetOrderLineViews(...)`
- [PostgresDataStore.cs:1721](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1721) `DeleteOrderLines(...)`
- [PostgresDataStore.cs:1732](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1732) `DeleteOrder(...)`
- [PostgresDataStore.cs:1792](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1792) `GetShippedTotalsByOrderLine(...)`
- [PostgresDataStore.cs:1572](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1572) `GetOrderReceiptRemaining(...)`
- [PostgresDataStore.cs:1627](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1627) `GetOrderShipmentRemaining(...)`
- [PostgresDataStore.cs:1826](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1826) `GetOrderShippedAt(...)`
- [PostgresDataStore.cs:1843](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1843) `HasOutboundDocs(...)`

### Order-bound document writes
Legacy local doc flows still exist in [DocumentService.cs](d:/FlowStock/apps/windows/FlowStock.Core/Services/DocumentService.cs):
- [DocumentService.cs:527](d:/FlowStock/apps/windows/FlowStock.Core/Services/DocumentService.cs:527) `ApplyOrderToDoc(...)`
  - `UpdateDocHeader()`
  - `UpdateDocOrder()`
  - `DeleteDocLines()`
  - `AddDocLine()` loop from `GetOrderShipmentRemaining()`
- [DocumentService.cs:577](d:/FlowStock/apps/windows/FlowStock.Core/Services/DocumentService.cs:577) `ApplyOrderToProductionReceipt(...)`
  - `UpdateDocOrder()`
  - optional `DeleteDocLines()`
  - `AddDocLine()` loop from `GetOrderReceiptRemaining()`
- [DocumentService.cs:636](d:/FlowStock/apps/windows/FlowStock.Core/Services/DocumentService.cs:636) `ClearDocOrder(...)`
- [DocumentService.cs:651](d:/FlowStock/apps/windows/FlowStock.Core/Services/DocumentService.cs:651) `UpdateDocOrderBinding(...)`

## Order identity and status model

### Identity
Source of truth is described in [spec_orders.md](d:/FlowStock/docs/spec_orders.md).
- Business identity: `orders.id`
- Business reference: `orders.order_ref`
- Lines: `order_lines.id`
- Order-to-document linkage: `docs.order_id`, `docs.order_ref`
- Line-to-lineage linkage: `doc_lines.order_line_id`

### Status model
Per [spec_orders.md](d:/FlowStock/docs/spec_orders.md):
- `DRAFT`
- `ACCEPTED`
- `IN_PROGRESS`
- `SHIPPED`

Manual status changes are intended only for `ACCEPTED` / `IN_PROGRESS`.
`SHIPPED` is automatic.

Current implementation is split:
- [OrderService.cs:257](d:/FlowStock/apps/windows/FlowStock.Core/Services/OrderService.cs:257) `ChangeOrderStatus(...)` handles manual status changes.
- [OrderService.cs:350](d:/FlowStock/apps/windows/FlowStock.Core/Services/OrderService.cs:350) `ApplyAutoStatus(...)` recalculates and persists status on read.

This means order status is not updated only at document close time. It is also updated opportunistically during order reads.

## Create outbound from order flow
A direct Orders-tab action "Create outbound from order" was not found.

Current operator flow is indirect:
1. create a generic draft document in the Operations area
2. open [OperationDetailsWindow.xaml.cs](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs)
3. bind an order via the order selector
4. use explicit `Заполнить из заказа`

There is also a programmatic path in [DocumentService.cs:33](d:/FlowStock/apps/windows/FlowStock.Core/Services/DocumentService.cs:33) `CreateDoc(..., orderId = null)` that can auto-prefill lines from an order if `orderId` is supplied, but a WPF Orders-tab entry point for that path was not found.

## Fill from order flow

### Outbound customer orders
- Explicit entry: [OperationDetailsWindow.xaml.cs:2434](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2434) `DocFillFromOrder_Click(...)`
- Branching: [OperationDetailsWindow.xaml.cs:2146](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2146) `TryApplyOrderSelectionAsync(...)`
- Server mode: [OperationDetailsWindow.xaml.cs:2332](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2332) `TryApplyOrderSelectionViaServerAsync(...)`
  - local `UpdateDocHeader()` / `UpdateDocOrderBinding()`
  - canonical delete old lines
  - canonical add new lines
- Legacy mode: [OperationDetailsWindow.xaml.cs:2176](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2176) `TryApplyOrderSelectionLegacy(...)`
  - local `ApplyOrderToDoc(...)`

### Internal orders / production receipt
- Explicit entry: same `DocFillFromOrder_Click(...)`
- Server mode: [OperationDetailsWindow.xaml.cs:2481](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2481) `FillProductionReceiptFromOrderAsync(...)`
- Legacy mode: same method branches to local `ApplyOrderToProductionReceipt(...)`

## Partial shipment flow

Outbound partial shipment behavior lives in [OperationDetailsWindow.xaml.cs](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs), not in the Orders tab.

- [OperationDetailsWindow.xaml.cs:2856](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:2856) `LoadOrderQuantities(...)` loads remaining quantities by `order_line_id`.
- [OperationDetailsWindow.xaml.cs:985](d:/FlowStock/apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs:985) and adjacent update-qty guards use `HasOrderBinding()` and `_isPartialShipment` to decide whether quantity edits are allowed.
- Current WPF cleanup already made this flow explicit:
  - full shipment mode -> fill from order is explicit-only
  - update qty is blocked with explicit user messaging when `partial = false`
  - partial mode allows line quantity adjustments through canonical `UpdateDocLine` where feature flags are enabled

## Remaining/shipped calculation

### Orders tab metrics
- [OrderDetailsWindow.xaml.cs:308](d:/FlowStock/apps/windows/FlowStock.App/OrderDetailsWindow.xaml.cs:308) `RefreshLineMetrics()` computes visible quantities.
- Customer orders:
  - availability from `_services.Orders.GetItemAvailability()`
  - shipped from `_services.Orders.GetShippedTotals(orderId)`
  - `remaining = qty_ordered - qty_shipped`
  - `can_ship_now = min(remaining, available)`
- Internal orders:
  - produced from `_services.Documents.GetOrderReceiptRemaining(orderId)`
  - `remaining = qty_ordered - qty_produced`

### Core service metrics
- [OrderService.cs:35](d:/FlowStock/apps/windows/FlowStock.Core/Services/OrderService.cs:35) `GetOrderLineViews(...)` loads DB views and then applies metrics.
- [OrderService.cs:278](d:/FlowStock/apps/windows/FlowStock.Core/Services/OrderService.cs:278) `ApplyLineMetrics(...)` centralizes line calculations.
- [PostgresDataStore.cs:1792](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1792) `GetShippedTotalsByOrderLine(...)` uses closed `OUTBOUND` docs.
- [PostgresDataStore.cs:1572](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1572) `GetOrderReceiptRemaining(...)` uses closed `PRODUCTION_RECEIPT` docs.
- [PostgresDataStore.cs:1627](d:/FlowStock/apps/windows/FlowStock.Data/PostgresDataStore.cs:1627) `GetOrderShipmentRemaining(...)` uses closed `OUTBOUND` docs.

These calculations are ledger/document-driven, not stored counters.

## Order status update flow

### Manual status changes
- [OrderService.cs:257](d:/FlowStock/apps/windows/FlowStock.Core/Services/OrderService.cs:257) `ChangeOrderStatus(...)`
- For web/PC accounts, requests are created via [Program.cs:697](d:/FlowStock/apps/windows/FlowStock.Server/Program.cs:697) `POST /api/orders/requests/status` and then approved in WPF via [IncomingRequestsWindow.xaml.cs:300](d:/FlowStock/apps/windows/FlowStock.App/IncomingRequestsWindow.xaml.cs:300).

### Automatic status changes
- [OrderService.cs:350](d:/FlowStock/apps/windows/FlowStock.Core/Services/OrderService.cs:350) `ApplyAutoStatus(...)` runs during `GetOrders()` / `GetOrder()` reads and may persist status changes directly with `_data.UpdateOrderStatus(...)`.
- Customer order auto-status depends on `HasOutboundDocs(...)` plus shipped totals.
- Internal order auto-status depends on receipt totals from closed production receipts.

This read-triggered persistence is one of the main architectural differences to keep in mind for migration.

## Differences between WPF and server-side logic

### Orders tab writes
- WPF Orders tab writes directly through `OrderService` + `PostgresDataStore`.
- Server exposes order read endpoints, but no canonical direct write API for create/update/delete.
- Server order request endpoints are intake only; final business write still happens in WPF.

### Order-bound document flows
- The document lifecycle around orders is much further along.
- In server mode, order-bound `Outbound` / `ProductionReceipt` fills already use canonical document APIs for line create/update/delete/close.
- However order binding/header writes are still local WPF writes around those flows.

### Status persistence
- Order auto-status is currently a side effect of reading orders.
- There is no canonical server-side event/application operation that owns order status transitions at document close time.

## Which flows already use canonical document API

These flows are already capable of going through canonical document APIs under feature flags:
- order-bound draft create around Operations flow (indirectly, through canonical `POST /api/docs` if the draft itself is created in server mode)
- order-bound `Outbound` explicit fill/rebuild using canonical delete + add
- order-bound `ProductionReceipt` fill/rebuild using canonical delete + add
- partial shipment quantity edits via canonical update
- line deletion via canonical delete
- line addition via canonical add
- close document via canonical close

Important boundary:
- these are document operations that happen around an order
- they are not a migrated Orders-tab write model

## Which flows still depend on legacy local writes

### Orders tab itself
These are still legacy local writes:
- create order from `OrderDetailsWindow`
- update order from `OrderDetailsWindow`
- delete order from `MainWindow`
- manual status change through `OrderService.ChangeOrderStatus(...)`
- WPF approval of `CREATE_ORDER` order requests
- WPF approval of `SET_ORDER_STATUS` order requests

### Order-bound document wrappers
These are still locally persisted even when line lifecycle is canonical:
- `UpdateDocHeader(...)` before order-bound fill
- `UpdateDocOrderBinding(...)`
- `ClearDocOrder(...)`
- legacy `ApplyOrderToDoc(...)` / `ApplyOrderToProductionReceipt(...)` when server batch mode flags are off

### Status auto-update on read
These are also local writes, even though they happen during read flows:
- `_data.UpdateOrderStatus(...)` from `ApplyAutoStatus(...)` during `GetOrders()` / `GetOrder()`

## Risks of migration

- Order status persistence is split between manual writes and read-triggered auto-writes. Migrating writes without resolving that ownership boundary can produce double-write or race behavior.
- `UpdateOrder(...)` is currently destructive/in-place for `order_lines` and also silently deduplicates by `item_id`. A future canonical server contract must either preserve or explicitly replace that behavior.
- Web order request approval currently converges into WPF local writes. If WPF Orders-tab writes migrate first, the request-approval path must be kept aligned.
- Order-bound document flows are already partially server-centric; migrating Orders-tab writes separately without documenting that boundary could create mixed semantics that are hard to reason about.
- `GenerateNextOrderRef()` exists both in WPF and server read API (`/api/orders/next-ref`). Ref generation ownership is already split.
- Direct deletion is still a hard delete (`DeleteOrderLines` + `DeleteOrder`). That is simpler than document append-only lifecycle, but it means order migration is not just a copy of the document migration pattern.

## Recommended first migration slice

Recommended first slice: **WPF CreateOrder from the Orders tab**.

Why this is the best high-value first slice:
- It is the narrowest write path with clear inputs and outputs.
- It stays inside the Orders tab boundary and does not require redesigning shipment calculations.
- It aligns naturally with the already existing server-side intake shape for `CREATE_ORDER` requests.
- It avoids the more complex destructive semantics of `UpdateOrder(...)`.
- It provides immediate value by removing one major direct-WPF-write path from the tab.

What not to take first:
- `UpdateOrder(...)` is riskier because current behavior mutates existing lines in place and also removes duplicates/stale lines.
- `DeleteOrder(...)` is smaller but lower value and tightly coupled to current business restrictions.
- automatic status migration is high-risk because status ownership is currently split and partially read-driven.

## Open questions

- Should the first canonical order write API be direct create (`POST /api/orders`) or should it reuse / evolve the existing `order_requests` intake model?
- Should `order_ref` become server-authored for WPF create, given that WPF currently generates it locally while server also exposes `/api/orders/next-ref`?
- Should order status remain persisted on read, or should status become strictly derived / write-owned by document close and explicit status operations?
- Is there any hidden WPF flow that creates an `Outbound` directly from the Orders tab? A direct UI action was not found in the inspected windows.
- Should `UpdateOrder(...)` preserve current line dedup-by-item behavior, or should a future canonical contract reject duplicate item rows explicitly?
- Should web request approval eventually call canonical server order APIs instead of local `OrderService` methods?
