# Strategy

`UpdateOrder` follows the same migration pattern already used for document lifecycle and `CreateOrder`:

1. define canonical server contract
2. lock expected behavior in a test matrix
3. implement direct HTTP integration tests
4. add WPF adapter-level compatibility tests
5. migrate Orders-tab save/update under feature flag

The authoritative state for this slice is:

- `orders`
- `order_lines`
- persisted `status`
- persisted `order_ref`
- absence of `docs` writes
- absence of `ledger` writes

# What is executable now

Executable now in `FlowStock.Server.Tests/UpdateOrder`:

- canonical update success
- header update
- validation failures
- `order_ref` collision replacement
- line snapshot replacement
- duplicate-line normalization by `item_id`
- state guarantees: no `docs`, no `ledger`
- WPF update bridge compatibility at service/adapter level

Files:

- `CanonicalUpdateIntegrationTests.cs`
- `ValidationTests.cs`
- `OrderRefTests.cs`
- `LineReplacementTests.cs`
- `StateGuaranteeTests.cs`
- `WpfCompatibilityTests.cs`

# What is deferred

Still deferred:

- `DeleteOrder`
- direct canonical `SetOrderStatus`
- `IncomingRequestsWindow` convergence
- idempotency / replay
- full UI automation for Orders tab

# Why it is deferred

These gaps are intentionally outside the first `UpdateOrder` slice:

1. the target here is direct canonical update only
2. `DeleteOrder` and status changes are separate write slices
3. request-intake approval flow should not be mixed into direct Orders-tab migration
4. idempotency requires a broader event/metadata design not needed for this slice

# How this supports later WPF Orders-tab migration

With `PUT /api/orders/{orderId}` stable and tested, WPF migration only needs to:

- collect the full editable order snapshot
- call canonical update endpoint under feature flag
- accept server-authoritative `order_ref`
- reload the order after save

That leaves later work focused on the remaining local order writes, not on redefining update semantics.
