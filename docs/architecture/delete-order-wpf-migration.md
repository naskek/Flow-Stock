# DeleteOrder WPF Migration

## Scope
- Только delete action во вкладке заказов.
- Create/update уже могут идти через server API по отдельным feature flags.

## Feature flag
- setting: `server.use_server_delete_order`
- env override: `FLOWSTOCK_USE_SERVER_DELETE_ORDER=true`

## Active path
- `false` -> legacy local delete through `OrderService.DeleteOrder(...)`
- `true` -> `MainWindow` -> `WpfDeleteOrderService` -> `DeleteOrderApiClient` -> `DELETE /api/orders/{orderId}`

## User-visible behavior
- подтверждение delete остаётся в WPF.
- при success список заказов перечитывается.
- при forbidden delete пользователь видит server-side validation message.
- при timeout / server unavailable пользователь видит transport error.

## Manual checklist
1. Legacy delete path
   - выключить `use_server_delete_order`
   - удалить draft order
   - проверить старое поведение
2. Server delete path
   - включить `use_server_delete_order`
   - удалить draft order
   - проверить, что заказ исчез из списка
3. Forbidden delete case
   - попытаться удалить заказ с forbidden state
   - проверить понятную ошибку
4. Timeout / server unavailable
   - остановить server
   - проверить transport error
5. Legacy rollback
   - выключить флаг
   - local delete path остаётся рабочим

## Remaining gaps
- `SetOrderStatus` ещё local
- `IncomingRequestsWindow` ещё local/intake-based
- order read-side остаётся DB-backed
- delete не имеет idempotency/replay
