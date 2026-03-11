# Purpose

This document records the targeted WPF cleanup for the `Outbound + order binding` scenario.

Goal:

- make `Fill from order` explicit in UI;
- remove silent UX blocks for line actions;
- disable implicit outbound fill/reapply triggers;
- keep current server contracts unchanged;
- improve local diagnostics when API is not reached.

# Explicit user flow

Primary WPF entry point:

- `apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs`

Updated behavior:

1. `Outbound` now shows the existing `Заполнить из заказа` button.
2. Clicking that button uses the already existing outbound fill logic:
   - full server rebuild mode -> `WpfDeleteDocLineService` + `WpfBatchAddDocLineService`
     - `POST /api/docs/{docUid}/lines/delete`
     - `POST /api/docs/{docUid}/lines`
   - legacy mode -> `DocumentService.ApplyOrderToDoc(...)`
3. `Outbound` order selection change no longer auto-fills/rebuilds lines.
4. `Save` no longer auto-fills/rebuilds outbound order-bound lines.
5. Turning partial shipment off no longer auto-reapplies order lines.
6. Manual `+ Товар` in `Outbound + order binding` remains product-blocked, but the user now gets an explicit message instead of only an implicit disabled state.
7. `Изменить количество...` in `Outbound + order binding + !partial` remains product-blocked, but now shows an explicit message instead of a silent early return.

# What changed in UX

## Explicit fill action for Outbound

Before:

- outbound fill was effectively hidden behind:
  - order selection
  - save header
  - partial-shipment toggle

Now:

- `FillFromOrderButton` is visible for `Outbound` as well;
- user has the only canonical action for line rebuild from order.
- changing order or toggling partial mode no longer mutates document lines.

## Manual `+ Товар`

Product rule remains:

- order-bound outbound does not allow manual line creation.

Now the user sees:

- `Для отгрузки с привязанным заказом ручное добавление строк отключено. Используйте 'Заполнить из заказа'.`

Additional UX change:

- the button is no longer silently disabled only because of order binding;
- the block is now visible and explained when the user attempts the action.

## `Изменить количество...`

Product rule remains:

- order-bound outbound line edit is allowed only in `Частичная отгрузка`.

Now the user sees:

- `Изменение количества доступно только в режиме 'Частичная отгрузка'. Для полного восстановления строк используйте 'Заполнить из заказа'.`

Additional UX change:

- the edit button is no longer silently disabled only because partial mode is off;
- the user can attempt the action and gets an explicit explanation.

## Order change, Save, partial toggle

Implicit mutating triggers removed for `Outbound`:

- order selection change
- save-triggered fill/reapply
- partial-toggle-triggered reapply

Current behavior:

- order change shows:
  - `Заказ изменён. Нажмите 'Заполнить из заказа', чтобы пересобрать строки.`
- save after order change shows:
  - `Параметры заказа сохранены. Нажмите 'Заполнить из заказа', чтобы пересобрать строки.`
- turning partial shipment off shows:
  - `Режим частичной отгрузки изменён. Нажмите 'Заполнить из заказа', чтобы пересобрать строки.`

# Server path usage

Relevant methods:

- `DocFillFromOrder_Click()`
- `TryApplyOrderSelectionAsync()`
- `TryApplyOrderSelectionViaServerAsync()`
- `WpfBatchAddDocLineService.IsServerBatchAddDocLineEnabled()`

If server mode is enabled:

- outbound explicit `Заполнить из заказа` routes to the existing server batch add-line path;
- each line is still appended through canonical `POST /api/docs/{docUid}/lines`;
- no server semantics were changed.

If server mode is disabled:

- outbound explicit `Заполнить из заказа` routes to the existing legacy local fill path.

# Diagnostic logging

Lightweight WPF diagnostics were added to `app.log` for outbound order-bound flows.

Logged cases:

- explicit fill-from-order button entry;
- `TryApplyOrderSelectionAsync()` branch selection;
- server batch branch preparation;
- location/HU validation failure before server call;
- manual add blocked by product rule;
- line update blocked by product rule;
- implicit trigger suppressed on order change;
- implicit trigger suppressed on save-bound order change;
- implicit trigger suppressed on partial toggle.

Log prefix:

- `wpf_outbound_order_bound`

Example messages:

```text
wpf_outbound_order_bound doc_id=42 order_id=1001 partial=False explicit fill-from-order button clicked: order_id=1001
```

```text
wpf_outbound_order_bound doc_id=42 order_id=1001 partial=False explicit fill routed to server batch path for order_id=1001
```

```text
wpf_outbound_order_bound doc_id=42 order_id=1001 partial=False implicit trigger suppressed: order selection changed; order_id=1001; fill/rebuild not invoked
```

```text
wpf_outbound_order_bound line_location_validation_failed caller=TryApplyOrderSelectionViaServerAsync doc_id=42 order_id=1001 partial=False from_location_id=- to_location_id=- from_hu=- to_hu=-
```

# Product-allowed vs blocked actions

Allowed:

- explicit `Заполнить из заказа`
- close document
- update quantity in partial shipment mode

Blocked by current product rules:

- manual `+ Товар` for order-bound outbound
- quantity edit when partial shipment mode is off

# Manual checklist

1. `Outbound + bound order + explicit Fill from order`
   - open outbound draft with selected order
   - click `Заполнить из заказа`
   - verify lines are rebuilt

2. `Order selection does not auto-fill`
   - open outbound draft
   - choose order
   - verify lines do not change automatically
   - verify user sees explicit hint to press `Заполнить из заказа`

3. `Save does not auto-rebuild`
   - change order/header parameters
   - click `Сохранить`
   - verify lines do not rebuild automatically
   - verify user sees explicit reminder if order binding changed

4. `Partial toggle does not auto-rebuild`
   - open order-bound outbound
   - turn partial off
   - verify lines do not rebuild automatically
   - verify user sees explicit reminder

5. `Legacy/server mode comparison`
   - disable `use_server_add_doc_line`
   - run `Заполнить из заказа`
   - enable `use_server_add_doc_line`
   - enable `use_server_delete_doc_line`
   - run `Заполнить из заказа`
   - compare visible result

6. `Manual + Товар blocked with explicit message`
   - open order-bound outbound
   - click `+ Товар`
   - verify user sees explicit block message

7. `Update qty blocked with explicit message when !partial`
   - open order-bound outbound
   - keep `Частичная отгрузка` disabled
   - select line
   - click `Изменить количество...`
   - verify user sees explicit block message

8. `Update qty works when partial=true`
   - open order-bound outbound
   - enable `Частичная отгрузка`
   - select line
   - click `Изменить количество...`
   - verify the operation can reach current update path

9. `Diagnostic log shows suppressed vs explicit paths`
   - change order
   - verify `app.log` contains `implicit trigger suppressed`
   - run explicit fill without source location
   - verify `app.log` contains `line_location_validation_failed`
   - run explicit fill with full server rebuild mode
   - verify server log contains both `DeleteDocLine` and `AddDocLine`
   - run explicit fill with valid source location in server mode
   - verify `app.log` contains branch-selection entry before server call

# Remaining hidden outbound branches

No hidden outbound fill/reapply branches remain in this scenario.

Still existing, but explicit and outside fill semantics:

- explicit `Заполнить из заказа`
- explicit `Сохранить` of header/order binding
- explicit `Сброс` of order binding

These may still mutate header/order metadata, but they no longer silently rebuild line sets.
