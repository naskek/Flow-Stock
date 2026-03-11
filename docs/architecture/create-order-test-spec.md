# Strategy

`CreateOrder` is now tested as a direct canonical server write contract:

- endpoint: `POST /api/orders`
- scope: direct order create only
- out of scope: `UpdateOrder`, `DeleteOrder`, direct `SetOrderStatus`, `order_requests` convergence

The test strategy follows the same pattern already used for document lifecycle migrations:

1. define the canonical server contract
2. define the test matrix
3. implement HTTP integration tests
4. harden production behavior until the contract tests are green
5. leave WPF compatibility as a separate migration layer

The authoritative state for this slice is:

- `orders`
- `order_lines`
- persisted `status`
- persisted `order_ref`
- absence of `docs` writes
- absence of `ledger` writes

# What is executable now

Executable now in `FlowStock.Server.Tests/CreateOrder`:

- canonical create success
- validation failures
- `order_ref` generation and collision replacement
- duplicate-line normalization by `item_id`
- state guarantees: no `docs`, no `ledger`

Current executable files:

- `CanonicalCreateIntegrationTests.cs`
- `ValidationTests.cs`
- `OrderRefTests.cs`
- `LineNormalizationTests.cs`
- `StateGuaranteeTests.cs`

Infrastructure now available:

- `CreateOrderHttpApi`
- `CreateOrderHttpScenario`
- `CreateOrder` collection fixture for deterministic partner-role file setup
- order-aware `CloseDocumentHarness` with `orders` and `order_lines` support

# What is deferred

Deferred to later slices:

- `IncomingRequestsWindow` convergence to canonical direct create
- `UpdateOrder`
- `DeleteOrder`
- direct canonical `SetOrderStatus`
- idempotency / replay for order create

# WPF compatibility coverage now

The WPF create bridge is now covered at adapter level through `WpfCreateOrderService`.

Executable WPF compatibility tests:

- `WpfCreateOrder_FeatureFlagRoutesToCanonicalPostApiOrders`
- `WpfCreateOrder_AcceptsServerAssignedOrderRef`
- `WpfLegacyCreatePath_RemainsAvailableUnderFeatureFlag`

# Why it is deferred

These gaps are intentionally outside the first direct server create slice:

1. request-intake approval flow is a separate workflow and should not be mixed into the first server create implementation
2. idempotency would require a broader metadata/event design that the current order slice intentionally avoids
3. `UpdateOrder`, `DeleteOrder` and direct `SetOrderStatus` are separate write slices

# How this supports later WPF CreateOrder migration

The server contract is now stable enough for WPF migration under a feature flag, and the first adapter bridge is now implemented.

When WPF migration starts, the bridge only needs to:

- collect header + lines from the modal
- call `POST /api/orders`
- accept server-authoritative `order_ref`
- accept returned `order_id`
- reload/open the created order by `order_id`

That means later Orders-tab work can focus on the remaining write paths and UX cleanup, not on redefining server semantics.

# Current implementation coverage summary

Covered and green now:

- `CUSTOMER` create
- `INTERNAL` create
- returned `order_id`
- returned authoritative `order_ref`
- returned persisted `status`
- generated `order_ref` when missing
- replacement `order_ref` on collision
- validation for partner, due date, lines, items, qty, `SHIPPED`
- line normalization by `item_id`
- absence of `docs` writes
- absence of `ledger` writes

Still intentionally not covered now:

- server/client timeout retry semantics
- request-intake approval convergence

# Notes for the next implementation phase

The next logical implementation step is no longer WPF create itself. It is one of:

- `UpdateOrder` migration
- `DeleteOrder` migration
- `IncomingRequestsWindow` convergence to canonical direct create

Direct server create and the WPF feature-flagged create bridge are already sufficiently hardened for that next step.
