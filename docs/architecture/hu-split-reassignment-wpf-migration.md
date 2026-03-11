# Purpose

This document describes the WPF migration state for outbound HU allocation / split / reassignment after moving those flows to canonical server line APIs in full server mode.

# Previously local HU flows

Before this step, WPF kept the following outbound HU mutations local:

- `OutboundHuApply_Click()` from the outbound HU candidate panel
- outbound branch of `AssignHuButton_Click()`

Local mutation sequence used:

- `DocumentService.DeleteDocLine()`
- `DocumentService.UpdateDocLineQty()`
- `DocumentService.AddDocLine()`

That mixed legacy local writes into canonical document lifecycle.

# Full server mode

Full server HU mode is enabled only when all three flags are on:

- `server.use_server_add_doc_line`
- `server.use_server_update_doc_line`
- `server.use_server_delete_doc_line`

If any flag is off, WPF keeps the legacy local fallback and writes a diagnostic reason to `app.log`.

# Canonical server sequence

In full server mode outbound HU split / reassignment now uses:

1. identify the active source line;
2. if the source line must disappear entirely, call canonical `DeleteDocLine`;
3. if the source line must shrink, call canonical `UpdateDocLine`;
4. create the resulting split / reassigned line through canonical `AddDocLine`;
5. reload document and line grid.

Important boundary:

- WPF does not mix local destructive writes with server writes inside this mode;
- server semantics remain append-only;
- `ledger` is still untouched until close;
- the sequence is not atomic end-to-end, because delete/update/add are still separate requests.

# Files involved

- `apps/windows/FlowStock.App/OperationDetailsWindow.xaml.cs`
  - `OutboundHuApply_Click()`
  - `AssignHuButton_Click()`
  - `TryApplyOutboundHuMutationViaServerAsync()`
  - `IsFullServerLineLifecycleEnabled()`
- `apps/windows/FlowStock.App/Services/WpfAddDocLineService.cs`
- `apps/windows/FlowStock.App/Services/WpfUpdateDocLineService.cs`
- `apps/windows/FlowStock.App/Services/WpfDeleteDocLineService.cs`

# User-visible behavior

Preserved:

- existing outbound HU UI flow
- legacy fallback when full server line lifecycle mode is not enabled

Changed:

- in full server mode the authoritative write path is now server-only;
- after success WPF reloads the whole document instead of pretending local mutations are authoritative;
- after partial failure WPF shows a message telling the operator to refresh and inspect actual lines before retrying.

# Diagnostics

WPF writes diagnostic entries with prefix:

- `wpf_hu_server_flow`

Typical messages:

- `... routed to legacy local path: add=True update=False delete=True`
- `... server sequence prepared: source_line_id=...`
- `... server update phase started ...`
- `... server delete phase started ...`
- `... server add phase failed ...`
- `... server sequence completed ...`

# Legacy exceptions after this step

Still local:

- non-outbound HU assignment flows
- production receipt HU auto-distribution
- JSONL import
- any outbound HU flow when full server line lifecycle mode is not enabled

# Manual checklist

1. Legacy mode
   - disable one or more of:
     - `use_server_add_doc_line`
     - `use_server_update_doc_line`
     - `use_server_delete_doc_line`
   - run outbound HU allocation / reassignment
   - verify old local behavior still works

2. Server mode
   - enable all three line flags
   - run outbound HU allocation / reassignment
   - verify:
     - lines are rebuilt
     - document stays `DRAFT`
     - `ledger` is not written

3. Logs
   - verify server log contains `UpdateDocLine`, `DeleteDocLine`, `AddDocLine`
   - verify WPF `app.log` contains `wpf_hu_server_flow`

4. Partial failure
   - simulate timeout / server unavailable in the middle of the sequence
   - verify WPF shows a partial-failure message
   - verify WPF reloads the document
   - inspect authoritative lines before retrying

5. End-to-end
   - create draft
   - fill / rebuild lines
   - run outbound HU split / reassignment
   - update one line
   - delete one line
   - close document
