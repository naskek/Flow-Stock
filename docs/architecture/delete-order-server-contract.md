# DeleteOrder Server Contract

## Purpose
- Зафиксировать canonical server-centric delete для orders tab.
- Повторить текущие локальные domain rules удаления без изменения business semantics.

## Current-state assumptions
- Локальный delete сейчас идёт через `OrderService.DeleteOrder(...)`.
- Текущая семантика: hard delete заказа и его строк.
- Read-side заказов остаётся DB-backed и вне этого slice.

## Canonical operation definition
- Операция: удалить заказ напрямую через server API.
- First slice не вводит soft-delete и не вводит idempotency/replay.
- Server повторяет current local validation rules и только потом вызывает existing `OrderService.DeleteOrder(...)`.

## Proposed endpoint
- `DELETE /api/orders/{orderId}`

## Response model
- `200 OK`
```json
{
  "ok": true,
  "result": "DELETED",
  "order_id": 123,
  "order_ref": "00123"
}
```

- `404 Not Found`
```json
{
  "ok": false,
  "error": "ORDER_NOT_FOUND"
}
```

- `400 Bad Request`
```json
{
  "ok": false,
  "error": "ORDER_DELETE_FORBIDDEN_STATUS"
}
```

## Allowed delete cases
- Заказ существует.
- Заказ в статусе `DRAFT`.
- Нет outbound docs / связанных отгрузок.
- Нет shipped quantities.
- Для `INTERNAL`:
  - нет `ProductionReceipt` docs по заказу;
  - нет уже полученного выпуска (`GetOrderReceiptRemaining(...).QtyReceived > tolerance`).

## Forbidden delete cases
- `order_id` не найден.
- Статус не `DRAFT`.
- Есть outbound docs / связанные документы.
- Есть shipped quantities.
- Для `INTERNAL` есть production docs.
- Для `INTERNAL` уже был production receipt.

## Validation rules
- `ORDER_NOT_FOUND`
- `ORDER_DELETE_FORBIDDEN_STATUS`
- `ORDER_HAS_OUTBOUND_DOCS`
- `ORDER_HAS_SHIPMENTS`
- `ORDER_HAS_PRODUCTION_DOCS`
- `ORDER_HAS_PRODUCTION_RECEIPTS`
- fallback: `ORDER_DELETE_FAILED`

## Guarantees
- `docs` не создаются и не изменяются.
- `ledger` не создаётся и не изменяется.
- При успешном delete заказ и его `order_lines` физически удалены, как и в current local semantics.

## Compatibility with WPF
- WPF delete action в orders tab может идти:
  - legacy local path
  - server path через feature flag
- В server mode WPF остаётся thin client:
  - подтверждает delete;
  - вызывает `DELETE /api/orders/{orderId}`;
  - refresh orders grid after success.

## Out of scope
- `SetOrderStatus`
- `IncomingRequestsWindow`
- order read-side migration
- idempotency/replay для orders
- soft-delete

## Test specification
- successful delete of allowed order
- unknown `order_id` fails
- non-draft order fails
- order with outbound docs fails
- order with shipped quantities fails
- internal order with production docs fails
- internal order with receipt progress fails
- no `docs` writes
- no `ledger` writes
- WPF feature-flag route
- WPF legacy fallback

## Decisions requiring confirmation
- Нужен ли later отдельный `DELETE_FORBIDDEN` envelope с richer details вместо current error code only.
- Нужен ли later soft-delete/audit trail для orders, если domain semantics изменятся.
