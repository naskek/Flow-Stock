# Purpose

This document audits the current WPF line-operation flow for `Outbound` documents with a bound order and explains why some user actions do not reach the canonical server API.

Scope:

- fill/rebuild lines from order
- manual `+ Товар`
- `Изменить количество...`

Primary code anchor:

- `apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs`

# Fill from order flow

## Actual outbound entry points

For `Outbound`, there is no dedicated visible `Заполнить из заказа` button flow.

Relevant methods:

- `DocOrderCombo_SelectionChanged()`
- `DocPartialCheck_Changed()`
- `TrySaveHeaderAsync()`
- `TryApplyOrderSelectionAsync()`
- `TryApplyOrderSelectionViaServerAsync()`
- `TryApplyOrderSelectionLegacy()`

Important UI detail:

- `FillFromOrderButton` is visible only for `ProductionReceipt`.
- In `UpdateDocView()`:
  - `FillFromOrderButton.Visibility = doc.Type == DocType.ProductionReceipt ? Visible : Collapsed`

So for `Outbound`, "fill from order" actually means:

1. user changes order in `DocOrderCombo`, or
2. user saves header while an order is selected, or
3. user turns off partial shipment and WPF re-applies the order lines.

## Server path

`TryApplyOrderSelectionAsync(selected)` is the main branch:

- if `_services.WpfBatchAddDocLines.IsServerBatchAddDocLineEnabled()` is `true`
  - it calls `TryApplyOrderSelectionViaServerAsync(selected)`
  - which builds contexts and then calls `WpfBatchAddDocLineService.AddLinesBatchAsync(...)`
  - which sends one `POST /api/docs/{docUid}/lines` per order line

## Early blocking before server call

`TryApplyOrderSelectionViaServerAsync(selected)` returns before API if:

- `_doc == null`
- `TryGetLineLocations(...)` fails
  - for `Outbound`, this requires source location
  - WPF shows `Для отгрузки выберите место хранения источника.`
- local pre-steps fail:
  - `UpdateDocHeader(...)`
  - `UpdateDocOrderBinding(...)`
  - `DeleteDocLines(...)`

## Why user may see no server logs

1. Opening an already order-bound outbound document does not re-run fill automatically.
   - `SelectOrderFromDoc()` loads the combo under `_suppressOrderSync = true`
   - therefore `DocOrderCombo_SelectionChanged()` is not triggered on load

2. If the order selection does not change, `DocOrderCombo_SelectionChanged()` does not fire.

3. If the header is not dirty, actions like manual add do not force `TrySaveHeaderAsync()`.

4. If source location is not selected, `TryGetLineLocations(...)` stops before server batch add.

# Manual add flow

Relevant method:

- `DocAddLine_Click()`

## Server path

The server path exists only after these guards:

- draft selected
- optional `TrySaveHeaderAsync()` succeeds if header is dirty
- `HasOrderBinding()` is `false`
- item picker and qty dialog succeed
- `TryGetLineLocations(...)` succeeds
- `_services.WpfAddDocLines.IsServerAddDocLineEnabled()` is `true`

Then WPF calls:

- `TryAddLineViaServerAsync(...)`
- `WpfAddDocLineService.AddLineAsync(...)`
- `POST /api/docs/{docUid}/lines`

## Where server API is bypassed

For `Outbound` with bound order:

- `DocAddLine_Click()` contains an unconditional early return:
  - `if (HasOrderBinding()) { MessageBox.Show(...); return; }`

This check happens before the feature flag branch.

So in order-bound outbound documents:

- server add-line path is not called
- legacy add-line path is not called either
- the operation is blocked in UI logic

## Additional UI gating

`UpdateLineButtons()` also disables the button:

- `AddItemButton.IsEnabled = isEditable && !hasOrder`

This means the user usually cannot click it in normal UI state at all.

Conclusion:

- manual add for order-bound outbound is blocked by design in WPF
- `use_server_add_doc_line` is never reached in this branch

# Update qty flow

Relevant method:

- `DocEditLine_Click()`

## Server path

The server update path exists only if WPF gets past these guards:

- draft selected
- not blocked by order-binding rule
- one line selected
- if order-bound, selected line must have `OrderLineId`
- if order-bound, `TryGetOrderedQty(...)` must succeed
- qty dialog succeeds
- partial shipment qty validation succeeds
- current line still exists
- `_services.WpfUpdateDocLines.IsServerUpdateEnabled()` is `true`

Then WPF calls:

- `WpfUpdateDocLineService.UpdateLineAsync(...)`
- `POST /api/docs/{docUid}/lines/update`

## Where server API is bypassed

For `Outbound` with bound order and not in partial mode:

- there is an early return before the flag check:
  - `if (HasOrderBinding() && !_isPartialShipment) { return; }`

So:

- `use_server_update_doc_line` is ignored in this branch
- neither server update nor legacy update is called

## Additional UI gating

`UpdateLineButtons()` disables edit in the same scenario:

- `EditLineButton.IsEnabled = isEditable && hasSingleSelection && (!hasOrder || allowPartialEdit)`
- where `allowPartialEdit = hasOrder && _isPartialShipment`

Therefore:

- order-bound outbound line edit is allowed only in partial shipment mode
- only in that mode can the server update endpoint be reached

# Feature flag usage

Relevant flags:

- `server.use_server_add_doc_line`
- `server.use_server_update_doc_line`

Where they are used:

- `WpfBatchAddDocLineService.IsServerBatchAddDocLineEnabled()`
- `WpfAddDocLineService.IsServerAddDocLineEnabled()`
- `WpfUpdateDocLineService.IsServerUpdateEnabled()`

Audit finding:

- the flags are wired correctly
- the main issue is not missing flag wiring
- the main issue is that outbound/order-bound UI branches often return before the flag checks

# Where server API is bypassed

## Outbound fill/rebuild

Bypassed when:

- user only opens an already bound doc
- user does not change the order selection
- header is not dirty
- source location is missing

Server batch add is available, but it is only triggered from specific wrapper paths.

## Outbound manual add

Always bypassed for order-bound outbound docs because:

- `HasOrderBinding()` hard-blocks the action before the flag/API branch

## Outbound update qty

Bypassed for non-partial order-bound outbound because:

- `HasOrderBinding() && !_isPartialShipment` returns before the flag/API branch

# Where UI blocks operation

Main UI blocking points:

1. `UpdateLineButtons()`
   - disables `AddItemButton` whenever outbound doc has order binding
   - disables `EditLineButton` unless partial shipment mode is active

2. `DocAddLine_Click()`
   - explicit message + return on `HasOrderBinding()`

3. `DocEditLine_Click()`
   - silent early return on `HasOrderBinding() && !_isPartialShipment`

4. `TryGetLineLocations(...)`
   - blocks outbound fill/rebuild before any server call if source location is not selected

# Minimal recommended fix points

1. Make outbound fill/rebuild explicit in UI
   - either expose a dedicated outbound `Fill from order` action
   - or clearly document that outbound fill happens only on order select/save/reapply

2. Add explicit user feedback for blocked outbound update
   - current `DocEditLine_Click()` silently returns for non-partial order-bound docs
   - this should at least show why the edit is blocked

3. Decide product rule for manual add in order-bound outbound
   - if manual add must remain forbidden, keep the block but make it explicit in UI/docs
   - if partial/manual deviations from order are allowed, move the block behind a more precise business rule instead of unconditional `HasOrderBinding()`

4. If the intended behavior is "server mode should still log an attempt", move feature-flagged server wrapper selection ahead of some current UI short-circuit returns and let the server reject invalid cases canonically.

5. For outbound order fill diagnostics, add a lightweight WPF-side log entry before calling `TryApplyOrderSelectionAsync(...)` and before returning from `TryGetLineLocations(...)`, so absence of server logs can be distinguished from "server never called".
