# SetOrderStatus Server Contract

## Purpose
- Зафиксировать canonical manual status change для orders tab.
- Повторить текущие local domain rules смены статуса без изменения business semantics.

## Current-state assumptions
- Direct manual status change в domain уже существует как `OrderService.ChangeOrderStatus(...)`.
- Orders read-side остаётся DB-backed.
- Incoming requests flow не входит в этот slice.

## Canonical operation definition
- Manual status change отдельным direct endpoint.
- First slice не вводит idempotency/replay.
- Server вызывает existing `OrderService.ChangeOrderStatus(...)` и возвращает явный результат.

## Proposed endpoint
- `POST /api/orders/{orderId}/status`

## Request DTO
```json
{
  "status": "ACCEPTED"
}
```

## Response DTO
```json
{
  "ok": true,
  "result": "STATUS_CHANGED",
  "order_id": 123,
  "status": "ACCEPTED"
}
```

## Allowed statuses
- `ACCEPTED`
- `IN_PROGRESS`

## Allowed transitions
- `DRAFT -> ACCEPTED`
- `DRAFT -> IN_PROGRESS`
- `ACCEPTED -> IN_PROGRESS`
- `ACCEPTED -> ACCEPTED`
- `IN_PROGRESS -> ACCEPTED`
- `IN_PROGRESS -> IN_PROGRESS`

## Forbidden transitions
- any manual transition to `SHIPPED`
- manual transition to `DRAFT`
- any manual transition from already `SHIPPED` order

## Validation rules
- `ORDER_NOT_FOUND`
- `INVALID_STATUS`
- `ORDER_STATUS_CHANGE_FORBIDDEN`
- `ORDER_STATUS_SHIPPED_FORBIDDEN`
- `ORDER_STATUS_INVALID_TARGET`
- fallback: `ORDER_STATUS_CHANGE_FAILED`

## Guarantees
- `docs` не создаются и не изменяются.
- `ledger` не создаётся и не изменяется.
- Меняется только `orders.status`.

## Compatibility with WPF
- WPF path остаётся feature-flagged.
- First slice подключён к `OrderDetailsWindow` только для status-only save:
  - если меняется только статус, а header/lines snapshot не менялись, окно идёт в direct status endpoint;
  - если меняются другие поля, остаётся existing update-order save path.

## Out of scope
- `IncomingRequestsWindow`
- idempotency/replay
- orders read-side migration
- create/update/delete semantics

## Test specification
- successful allowed transition
- unknown order id fails
- invalid status fails
- shipped target forbidden
- draft target forbidden
- changing already shipped order forbidden
- response returns `order_id/status`
- no docs writes
- no ledger writes
- WPF feature-flag routing
- WPF legacy fallback

## Decisions requiring confirmation
- Нужен ли later explicit `NO_OP` result для same-status requests вместо текущего always-success `STATUS_CHANGED`.
- Нужен ли later отдельный direct status action в `MainWindow`, а не только status-only save из `OrderDetailsWindow`.
