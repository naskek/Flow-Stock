# Purpose

This document defines the canonical server-centric contract for the first Orders-tab migration slice in FlowStock:

- `CreateOrder` from WPF Orders tab

It is based on:

- `docs/architecture/orders-tab-current-state.md`

Goal of this document:

- define one pragmatic canonical server write contract for order creation;
- keep the scope intentionally narrow to the selected first migration slice;
- preserve current business semantics where they are already clear and useful;
- make the next step testable without redesigning the whole orders module.

This document does not change production code. It defines the target contract for migration planning.

# Current-state assumptions used for design

The following assumptions come from the current-state audit and inspected code.

1. Orders-tab create is currently local-only:
   - `apps/windows/FlowStock.App/OrderDetailsWindow.xaml.cs`
   - `TrySaveOrder()` -> `OrderService.CreateOrder(...)`

2. The current business create primitive already exists:
   - `apps/windows/FlowStock.Core/Services/OrderService.cs`
   - `CreateOrder(string orderRef, long? partnerId, DateTime? dueDate, OrderStatus status, string? comment, IReadOnlyList<OrderLineView> lines, OrderType type = OrderType.Customer)`

3. The current write persistence already exists and is simple:
   - `orders` row insert
   - `order_lines` inserts
   - no ledger writes
   - no document writes

4. Server already has order read endpoints:
   - `GET /api/orders`
   - `GET /api/orders/next-ref`
   - `GET /api/orders/{orderId}`
   - `GET /api/orders/{orderId}/lines`

5. Server already has order request intake endpoints, but they are not canonical direct create:
   - `POST /api/orders/requests/create`
   - `POST /api/orders/requests/status`

6. Incoming order requests are currently approved in WPF and still call local order writes:
   - `apps/windows/FlowStock.App/IncomingRequestsWindow.xaml.cs`
   - `ApplyOrderRequest()` -> `_services.Orders.CreateOrder(...)`

7. WPF create currently saves header and lines together in one modal save action.

8. Current WPF create allows explicit initial statuses except final shipped:
   - `DRAFT`
   - `ACCEPTED`
   - `IN_PROGRESS`
   - `SHIPPED` forbidden

9. Current `CreateOrder(...)` normalizes duplicate items by summing `qty_ordered` per `item_id` before persistence:
   - `OrderService.NormalizeLines(...)`

10. There is currently no order-lifecycle metadata layer equivalent to `api_docs` / `api_events` for documents.

11. Order status auto-updates on read exist today, but they are out of scope for this first create slice.

# Canonical operation definition

The canonical server-centric `CreateOrder` operation is:

`Create one persisted order with its initial order lines on the server, returning the authoritative order identity and authoritative order_ref, without writing ledger and without creating or closing documents.`

Canonical create is a business write operation for the Orders tab, not a request-intake workflow and not a document lifecycle operation.

Authoritative outcome state:

- one row in `orders`
- one or more rows in `order_lines`
- no `ledger` writes
- no `docs` writes
- no automatic shipment/production side effects

# Proposed endpoint

Canonical direct endpoint:

`POST /api/orders`

Decision:

- `CreateOrder` should use a direct canonical server endpoint, not route through existing request-intake endpoint semantics.

Reasoning:

- the selected migration slice is WPF Orders-tab create, not remote approval intake;
- request intake endpoints represent a separate workflow (`pending request -> approval`), not the authoritative direct create contract;
- forcing WPF create through request intake would add approval semantics where none currently exist;
- a direct endpoint maps cleanly to the existing local business primitive `OrderService.CreateOrder(...)`.

Role of existing intake endpoints after this slice:

- they remain valid for PC/web request submission;
- they remain outside the canonical direct-create contract;
- their approval path should later converge on the same application command as `POST /api/orders`.

# Proposed application command

Proposed canonical server-side application command:

`CreateOrderCommand`

Proposed shape:

```text
CreateOrderCommand
- requested_order_ref: string?
- type: OrderType
- partner_id: long?
- due_date: DateOnly?
- initial_status: OrderStatus?
- comment: string?
- lines: IReadOnlyList<CreateOrderLine>
- source: string?            // WPF | REQUEST_APPROVAL | API
```

`CreateOrderLine`

```text
CreateOrderLine
- item_id: long
- qty_ordered: double
```

Design intent:

- keep the first slice narrow and map directly onto the current create behavior;
- keep header + lines in one operation, because current WPF save already works that way;
- leave update/delete/status migration for later slices.

Closest implementation anchor in current code:

- `apps/windows/FlowStock.Core/Services/OrderService.cs`
- method `CreateOrder(...)`

# Request DTO

Target request DTO for `POST /api/orders`:

```json
{
  "order_ref": "optional-requested-ref",
  "type": "CUSTOMER",
  "partner_id": 123,
  "due_date": "2026-03-31",
  "status": "DRAFT",
  "comment": "optional comment",
  "lines": [
    {
      "item_id": 1001,
      "qty_ordered": 120.0
    },
    {
      "item_id": 1002,
      "qty_ordered": 45.0
    }
  ]
}
```

Field rules:

- `order_ref`
  - optional requested reference
  - if omitted, server generates authoritative `order_ref`
  - if supplied, server may accept it or replace it according to collision rules
- `type`
  - required
  - expected values: `CUSTOMER` or `INTERNAL`
- `partner_id`
  - required for `CUSTOMER`
  - omitted for `INTERNAL`
- `due_date`
  - optional
  - date-only in `yyyy-MM-dd`
- `status`
  - optional but explicit is preferred
  - allowed values at create time: `DRAFT`, `ACCEPTED`, `IN_PROGRESS`
  - `SHIPPED` forbidden
- `comment`
  - optional
- `lines`
  - required
  - one request creates header and lines together

Not included in canonical direct create DTO for v1:

- `device_id`
- `login`
- `event_id`
- request approval metadata

Those fields belong to request-intake workflow, not to the first direct WPF create slice.

# Response DTO

Target response DTO:

```json
{
  "ok": true,
  "result": "CREATED",
  "order_id": 501,
  "order_ref": "004",
  "order_ref_changed": false,
  "type": "CUSTOMER",
  "status": "DRAFT",
  "line_count": 2
}
```

Recommended fields:

- `ok`
- `result`
  - target value for this slice: `CREATED`
- `order_id`
  - authoritative technical identity
- `order_ref`
  - authoritative persisted business reference
- `order_ref_changed`
  - `true` when client requested a ref but server replaced it
- `type`
- `status`
- `line_count`

Validation failure response should stay consistent with current API style:

```json
{
  "ok": false,
  "error": "SOME_ERROR_CODE"
}
```

# Order identity model

Authoritative identity of a newly created order:

- `orders.id`

Decision:

- the authoritative identity of a newly created order should be `order_id`, not `order_ref`.

Reasoning:

- current business logic and DB interactions already key on `orders.id`;
- `order_ref` is a business reference and can be user-requested or server-replaced;
- unlike the document draft lifecycle, there is no existing need here for a separate remote identity like `doc_uid`.

Practical rule:

- WPF should create through `POST /api/orders`
- receive `order_id`
- then open or reload the order using `order_id`

# order_ref behavior

Decision:

- `order_ref` should be server-authoritative, with optional client-provided requested value.

Target behavior:

1. if client omits `order_ref`
   - server generates the next authoritative ref
2. if client provides an unused `order_ref`
   - server may accept it as-is
3. if client provides a colliding `order_ref`
   - server resolves the collision by assigning a different authoritative `order_ref`
   - response returns the final value and `order_ref_changed = true`

Reasoning:

- this matches the migration direction already chosen for document creation;
- it lets WPF keep a prefilled suggestion while moving authority to the server;
- it avoids relying on a stale local uniqueness check in WPF.

Compatibility note:

- current WPF `GenerateNextOrderRef()` and `TryValidateOrderRefUnique()` become advisory UX only, not authoritative enforcement.

# Initial status semantics

Decision:

- initial status should be explicit and deterministic.

Target rule:

- if `status` is provided, server validates it and persists it
- if `status` is omitted, server defaults to `DRAFT`
- `SHIPPED` is never allowed at create time

Allowed create-time statuses for v1:

- `DRAFT`
- `ACCEPTED`
- `IN_PROGRESS`

Reasoning:

- this preserves current WPF create behavior with minimal UX disruption;
- it keeps approval-created orders compatible with the current behavior where approved requests become `ACCEPTED`;
- it avoids bundling status-migration redesign into the first create slice.

Important boundary:

- automatic later transition to `SHIPPED` remains out of scope for this slice
- current read-time auto-status behavior is not changed by this contract

# Line creation semantics

Decision:

- `CreateOrder` should include header and lines in the same request.

Reasoning:

- current WPF save already creates a complete order in one action;
- introducing separate line-create endpoints before the first orders-tab migration would add unnecessary complexity;
- the current local business primitive already expects the full order shape at create time.

Target line semantics:

- request must include at least one valid line
- each line must specify `item_id` and `qty_ordered > 0`
- duplicate `item_id` values are allowed in transport
- server normalizes duplicate items by summing `qty_ordered` per `item_id` before persistence
- persisted order should contain at most one `order_lines` row per item after create

This preserves the behavior of `OrderService.NormalizeLines(...)` and keeps migration risk low.

# Validation rules

Canonical validation for `POST /api/orders`:

- `type` is required and valid
- `order_ref`, if present, must not be blank after trimming
- `status`, if present, must be one of `DRAFT`, `ACCEPTED`, `IN_PROGRESS`
- `SHIPPED` must be rejected
- `lines` must contain at least one valid line
- each line must have:
  - valid `item_id`
  - existing item
  - `qty_ordered > 0`
- `CUSTOMER` order must have valid `partner_id`
- selected customer partner must exist
- supplier-only partner must be rejected for customer order, matching current WPF/business rules
- `INTERNAL` order must not require a customer partner
- `due_date`, if present, must parse as `yyyy-MM-dd`

Not part of create-time validation for this slice:

- shipment/remaining logic
- order status auto-transition to `SHIPPED`
- outbound/production document creation
- partial shipment validation

# Idempotency / replay behavior

Decision:

- idempotency should **not** be introduced immediately for the first `CreateOrder` slice.

Reasoning:

- the orders module does not currently have a document-like metadata layer (`api_docs`, `api_events`) for direct order writes;
- adding full replay/idempotency infrastructure now would widen the slice beyond the chosen narrow first step;
- a direct canonical endpoint is already a large enough migration step for Orders tab create.

Pragmatic v1 behavior:

- `POST /api/orders` is create-only for this slice
- no `event_id`
- no canonical replay semantics in v1
- timeout recovery in WPF should be operationally handled by reload/search, not by optimistic blind retry semantics

Important consequence:

- the first create slice is intentionally weaker than document lifecycle in this one dimension;
- replay/idempotency can be introduced later if order create becomes a remote/mobile workflow rather than a mostly WPF-local operator action.

# Compatibility with WPF orders tab

Current WPF create flow:
- modal `OrderDetailsWindow`
- local `GenerateNextOrderRef()`
- local uniqueness precheck
- local `_lines` editing until save
- single save action creates header + lines together

Target migration direction:

- WPF should migrate toward the canonical API contract, not vice versa.

Practical WPF migration shape:

1. add `CreateOrderApiClient`
2. add `WpfCreateOrderService`
3. feature-flag WPF Orders-tab create to call `POST /api/orders`
4. on success:
   - accept server-authored `order_ref`
   - accept returned `order_id`
   - reload/open by `order_id`
5. on validation error:
   - show server error
6. on timeout/server unavailable:
   - show transport error and recommend refresh/search before retry

WPF-specific checks that may remain client-side as UX helpers:
- unsaved-changes prompt
- modal line editing convenience
- local preview of next order ref

Authoritative rules that move to server:
- final `order_ref`
- final validation
- final create persistence

# Compatibility with incoming requests flow

Existing request intake endpoints stay valid:
- `POST /api/orders/requests/create`
- `POST /api/orders/requests/status`

They are **not** the canonical direct create path.

Relationship to canonical create:

- request intake remains a separate approval workflow
- on approval, the workflow should eventually converge on the same application command as direct `POST /api/orders`

Pragmatic migration note for `IncomingRequestsWindow`:

- first migrate direct WPF Orders-tab create
- later migrate `ApplyOrderRequest(CREATE_ORDER)` to call canonical create instead of local `OrderService.CreateOrder(...)`

This keeps scope under control while still defining one authoritative create contract.

# Migration notes

Recommended migration sequence after this contract document:

1. prepare `create-order-test-matrix.md`
2. add server-side integration tests for `POST /api/orders`
3. implement the direct endpoint in server using the existing `OrderService.CreateOrder(...)` primitive as the first anchor
4. add WPF client/service under feature flag
5. migrate `IncomingRequestsWindow` approval path to the same canonical create command

Scope control rule:

- do not migrate `UpdateOrder`, `DeleteOrder`, or `SetOrderStatus` in the same implementation step

# Out of scope for this slice

Explicitly out of scope:

- `UpdateOrder`
- `DeleteOrder`
- direct canonical `SetOrderStatus`
- order request approval redesign
- order auto-status recalculation redesign
- creating outbound documents directly from Orders tab
- line-level order editing after create as a separate API
- replay/idempotency infrastructure for order create

# Test specification

Future integration tests for canonical `POST /api/orders` should cover:

## Core create
- successful create of `CUSTOMER` order with header + lines
- successful create of `INTERNAL` order with header + lines
- response returns `order_id`
- response returns authoritative `order_ref`
- response returns `status`
- `orders` row created exactly once
- `order_lines` rows created correctly
- no `ledger` rows written
- no `docs` rows created

## order_ref behavior
- missing `order_ref` -> server generates next ref
- requested free `order_ref` -> accepted as-is
- requested colliding `order_ref` -> replaced and `order_ref_changed = true`

## validation
- missing/invalid `type`
- customer order without partner fails
- customer order with supplier partner fails
- unknown partner fails
- invalid due date fails
- missing lines fails
- unknown item fails
- `qty_ordered <= 0` fails
- `SHIPPED` status fails

## line normalization
- duplicate same-item lines are normalized into one persisted line per item with summed quantity

## compatibility tests
- WPF create dialog can submit header + lines in one request
- WPF accepts server-replaced `order_ref`
- create result can be reopened through existing order read path using returned `order_id`
- future incoming request approval path converges to the same create semantics

# Decisions requiring confirmation

- Should `POST /api/orders` support both `CUSTOMER` and `INTERNAL` from day one, or should `INTERNAL` stay WPF-local for the first implementation step?
- For colliding `order_ref`, should the endpoint auto-replace or hard-fail? Preferred direction in this contract is auto-replace.
- Should `IN_PROGRESS` remain allowed at create time, or should canonical create narrow create-time statuses to `DRAFT` / `ACCEPTED` only?
- Is it acceptable for the first create slice to ship without replay/idempotency semantics?
- When migrating `IncomingRequestsWindow`, should approval call HTTP `POST /api/orders` or an in-process canonical application command that the HTTP endpoint also uses?
- Should local WPF duplicate-order-ref precheck remain as a pure UX hint, or be removed once server-authoritative ref assignment exists?
