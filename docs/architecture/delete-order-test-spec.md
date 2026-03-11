# DeleteOrder Test Spec

## Strategy
- Проверять canonical server delete отдельно от `order_requests`.
- Использовать реальные HTTP integration tests на `DELETE /api/orders/{orderId}`.
- Для WPF compatibility использовать adapter-level tests на `WpfDeleteOrderService`.

## What is executable now
- canonical delete success
- forbidden delete validations
- state guarantees: no docs writes, no ledger writes
- WPF feature-flag route
- WPF validation surfacing
- WPF legacy fallback

## What is deferred
- idempotency/replay
- read-side migration
- `IncomingRequestsWindow`
- `SetOrderStatus`

## Why
- этот slice нужен только для удаления заказа из orders tab без широкого refactor orders module.
- current domain already has stable local delete rules; server endpoint only mirrors them.

## How this supports later WPF migration
- orders tab delete action теперь можно перевести на server path под feature flag.
- после этого из write paths orders tab остаётся главным образом `SetOrderStatus` и incoming-requests convergence.
