# SetOrderStatus WPF Migration

## Scope
- Только manual status change из orders tab.
- First slice подключён в `OrderDetailsWindow` для status-only save.

## Feature flag
- setting: `server.use_server_set_order_status`
- env override: `FLOWSTOCK_USE_SERVER_SET_ORDER_STATUS=true`

## WPF route
- `false` -> legacy local `OrderService.ChangeOrderStatus(...)` при status-only save
- `true` -> `OrderDetailsWindow` -> `WpfSetOrderStatusService` -> `SetOrderStatusApiClient` -> `POST /api/orders/{orderId}/status`

## Canonical user flow
1. открыть существующий заказ
2. изменить только статус
3. сохранить
4. окно перечитывает заказ

Если меняются и другие поля, status-only path не используется; остаётся обычный update-order save flow.

## User-visible behavior
- при success окно перечитывает заказ и показывает стандартный `Сохранено`
- при forbidden transition пользователь видит server-side validation message
- при timeout / server unavailable пользователь видит transport error

## Manual checklist
1. Legacy status path
   - выключить `use_server_set_order_status`
   - изменить только статус
   - сохранить
   - проверить старое поведение
2. Server status path
   - включить `use_server_set_order_status`
   - изменить только статус
   - сохранить
   - проверить, что статус обновился
3. Forbidden transition
   - попытаться выставить запрещённый manual status
   - проверить понятную ошибку
4. Invalid status
   - проверить validation error через integration tests / direct API call
5. Timeout / server unavailable
   - остановить server
   - проверить transport error
6. Legacy rollback
   - выключить флаг
   - local status-change path остаётся рабочим

## Remaining gaps
- `IncomingRequestsWindow` now has separate feature-flagged convergence for `SET_ORDER_STATUS` approvals via canonical `POST /api/orders/{orderId}/status`
- orders read-side остаётся DB-backed
- status path пока без idempotency/replay
