# SetOrderStatus Test Spec

## Strategy
- Проверять direct server endpoint `POST /api/orders/{orderId}/status` отдельно от update-order snapshot flow.
- Использовать реальные HTTP integration tests для server contract.
- Для WPF compatibility использовать adapter-level tests на `WpfSetOrderStatusService`.

## What is executable now
- canonical success
- validation and transition-rule failures
- state guarantees: no `docs`, no `ledger`
- WPF feature-flag route
- WPF forbidden-transition surfacing
- WPF legacy fallback

## What is deferred
- idempotency/replay
- `IncomingRequestsWindow`
- orders read-side migration
- отдельный status action outside `OrderDetailsWindow`

## Why
- текущий slice закрывает только manual status change из orders tab без refactor всего orders module.
- direct status endpoint нужен как отдельный thin server-authoritative path, не смешанный с full snapshot update.

## How this supports later migration
- после этого у orders tab все основные direct writes migrated:
  - create
  - update
  - delete
  - manual status change
- следующим отдельным узлом остаётся `IncomingRequestsWindow`.
