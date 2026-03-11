# Purpose

This matrix fixes the expected behavior for the first Orders-tab migration slice:

- canonical `CreateOrder`
- `POST /api/orders`

The matrix is intentionally scoped to direct order creation from WPF Orders tab. It does not cover:

- `UpdateOrder`
- `DeleteOrder`
- direct `SetOrderStatus`
- `order_requests` approval flow

# Test matrix

| Test ID | Category | Scenario name | Preconditions | Request | Expected response | Authoritative state to verify | Automation level | Client relevance |
|---|---|---|---|---|---|---|---|---|
| CO-CAN-001 | Canonical create success | Successful create CUSTOMER | Existing customer partner and items | `POST /api/orders` with `type=CUSTOMER`, valid partner, valid lines | `200 OK`, `ok=true`, `result=CREATED`, returns `order_id/order_ref/status` | `orders` row exists, `order_lines` rows exist, status persisted, no `docs`, no `ledger` | integration | WPF, future API clients |
| CO-CAN-002 | Canonical create success | Successful create INTERNAL | Existing items | `POST /api/orders` with `type=INTERNAL`, valid lines | `200 OK`, `ok=true`, `result=CREATED` | `orders` row exists, `order_lines` rows exist, status persisted, no `docs`, no `ledger` | integration | WPF, future API clients |
| CO-CAN-003 | Canonical create success | Response returns authoritative identity | Existing valid create context | Valid create request | `order_id > 0`, authoritative `order_ref`, `status` | returned `order_id` resolves to persisted order | integration | WPF |
| CO-VAL-001 | Validation | Customer without partner fails | Existing items | `type=CUSTOMER`, no `partner_id`, valid lines | `400 BadRequest`, validation error | no `orders`, no `order_lines`, no `docs`, no `ledger` | integration | WPF |
| CO-VAL-002 | Validation | Supplier partner fails | Supplier partner exists | `type=CUSTOMER`, supplier partner, valid lines | `400 BadRequest`, validation error | no `orders`, no side effects | integration | WPF |
| CO-VAL-003 | Validation | Unknown partner fails | Items exist, no such partner | `type=CUSTOMER`, unknown `partner_id` | `400 BadRequest` | no `orders`, no side effects | integration | WPF |
| CO-VAL-004 | Validation | Invalid due date fails | Valid partner/items | invalid `due_date` format | `400 BadRequest` | no `orders`, no side effects | integration | WPF, future API clients |
| CO-VAL-005 | Validation | Missing lines fails | Valid partner/items | empty `lines` | `400 BadRequest` | no `orders`, no side effects | integration | WPF |
| CO-VAL-006 | Validation | Unknown item fails | Valid partner, missing item | line with unknown `item_id` | `400 BadRequest` | no `orders`, no side effects | integration | WPF |
| CO-VAL-007 | Validation | Non-positive quantity fails | Valid partner/item | line `qty_ordered <= 0` | `400 BadRequest` | no `orders`, no side effects | integration | WPF |
| CO-VAL-008 | Validation | SHIPPED status forbidden on create | Valid partner/items | `status=SHIPPED` | `400 BadRequest` | no `orders`, no side effects | integration | WPF, future API clients |
| CO-REF-001 | order_ref | Missing order_ref generates server ref | Valid partner/items | request without `order_ref` | `200 OK`, authoritative generated `order_ref`, `order_ref_changed=false` | persisted order uses returned ref | integration | WPF |
| CO-REF-002 | order_ref | Requested free order_ref accepted | Valid partner/items, ref unused | request with unused `order_ref` | covered by canonical create success with explicit free `order_ref` | persisted order uses requested ref | integration | WPF |
| CO-REF-003 | order_ref | Colliding requested order_ref replaced | Existing order with same ref | request with colliding `order_ref` | `200 OK`, replacement `order_ref`, `order_ref_changed=true` | persisted order uses returned replacement ref | integration | WPF |
| CO-LINE-001 | Line normalization | Duplicate items normalize server-side | Valid partner/items | request with duplicate `item_id` lines | `200 OK`, `line_count` reflects normalized result | one persisted `order_lines` row per item, summed qty | integration | WPF |
| CO-STATE-001 | State guarantees | Create writes no docs | Valid create context | successful create | `200 OK` | `docs` count unchanged | integration | WPF |
| CO-STATE-002 | State guarantees | Create writes no ledger | Valid create context | successful create | `200 OK` | `ledger` count unchanged | integration | WPF |
| CO-WPF-001 | WPF compatibility boundary | WPF create bridge routes to canonical endpoint | Feature flag on, WPF create service configured for server path | header + lines through `WpfCreateOrderService` | same create semantics as canonical endpoint | returned `order_id/order_ref/status` accepted by WPF adapter | compatibility integration | WPF |
| CO-WPF-002 | WPF compatibility boundary | WPF accepts server-assigned order_ref | Feature-flagged WPF bridge, requested ref missing or colliding | create through `WpfCreateOrderService` | WPF result exposes authoritative server ref | reopened order can use returned `order_id/order_ref` | compatibility integration | WPF |
| CO-WPF-003 | WPF compatibility boundary | Legacy local create remains available until migration cutover | Feature flag off | create through `WpfCreateOrderService` adapter call | adapter reports feature disabled and no server side effect | local path can remain authoritative until cutover | compatibility integration | WPF |
| CO-REQ-001 | IncomingRequests boundary | Direct create tests stay separate from request intake flow | none | none | none | no `order_requests` involved in canonical direct create suite | documented boundary | IncomingRequests |

# Notes

- `POST /api/orders` now exists in production for the direct canonical create slice.
- Server integration coverage is green for create, validation, `order_ref`, normalization, and state guarantees.
- WPF compatibility is now covered at adapter level through `WpfCreateOrderService`; full UI automation remains out of scope.
