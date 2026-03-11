# Purpose

This matrix defines the first executable coverage for canonical `UpdateOrder`.

Scope:

- `PUT /api/orders/{orderId}`
- WPF Orders-tab update bridge

Out of scope:

- `DeleteOrder`
- direct `SetOrderStatus`
- `IncomingRequestsWindow`
- idempotency / replay

| Test ID | Category | Scenario name | Preconditions | Request | Expected response | Authoritative state to verify | Automation level | Client relevance |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UO-CAN-001 | Canonical update success | Successful update of existing order | Existing editable order, valid partner/items | `PUT /api/orders/{orderId}` with valid snapshot | `200 OK`, `ok=true`, `result=UPDATED` | `orders` row updated, `order_lines` snapshot updated | integration | WPF, future API clients |
| UO-CAN-002 | Canonical update success | Response returns authoritative identity | Existing editable order | valid update request | returned `order_id/order_ref/status` | returned `order_id` resolves to persisted order | integration | WPF |
| UO-CAN-003 | Canonical update success | Header update works | Existing editable order | valid update with changed partner/date/status/comment | `200 OK` | updated header fields persisted | integration | WPF |
| UO-VAL-001 | Validation | Unknown order id fails | no such order | valid update payload to missing `orderId` | `404 NotFound`, validation error | no side effects | integration | WPF |
| UO-VAL-002 | Validation | Missing lines fails | Existing editable order | empty `lines` | `400 BadRequest` | existing order unchanged | integration | WPF |
| UO-VAL-003 | Validation | Unknown partner fails | Existing editable order | unknown `partner_id` | `400 BadRequest` | existing order unchanged | integration | WPF |
| UO-VAL-004 | Validation | Supplier partner fails | Existing editable order, supplier partner exists | supplier `partner_id` | `400 BadRequest` | existing order unchanged | integration | WPF |
| UO-VAL-005 | Validation | Invalid due date fails | Existing editable order | invalid `due_date` | `400 BadRequest` | existing order unchanged | integration | WPF |
| UO-VAL-006 | Validation | SHIPPED status forbidden | Existing editable order | `status=SHIPPED` | `400 BadRequest` | existing order unchanged | integration | WPF |
| UO-VAL-007 | Validation | Existing shipped order is not editable | Existing shipped order | otherwise valid update | `400 BadRequest` | existing shipped order unchanged | integration | WPF |
| UO-REF-001 | order_ref | Colliding requested order_ref replaced | Another order already uses requested ref | valid update with colliding `order_ref` | `200 OK`, replacement `order_ref`, `order_ref_changed=true` | persisted order uses returned replacement ref | integration | WPF |
| UO-LINE-001 | Line replacement | Snapshot replacement works | Existing order with multiple lines | valid update with different line snapshot | `200 OK` | stale lines removed, incoming lines persisted | integration | WPF |
| UO-LINE-002 | Line replacement | Duplicate item lines normalize | Existing editable order | update with duplicate `item_id` rows | `200 OK` | one persisted line per item, summed qty | integration | WPF |
| UO-STATE-001 | State guarantees | Update writes no docs | Existing editable order | successful update | `200 OK` | `docs` count unchanged | integration | WPF |
| UO-STATE-002 | State guarantees | Update writes no ledger | Existing editable order | successful update | `200 OK` | `ledger` count unchanged | integration | WPF |
| UO-WPF-001 | WPF compatibility | Feature flag routes update to canonical endpoint | Feature flag on | full snapshot through `WpfUpdateOrderService` | same semantics as canonical endpoint | returned `order_id/order_ref/status` accepted by WPF adapter | compatibility integration | WPF |
| UO-WPF-002 | WPF compatibility | WPF accepts server-replaced order_ref | Feature flag on, colliding requested ref | update through `WpfUpdateOrderService` | WPF receives authoritative replacement ref | reopened order can use returned `order_id/order_ref` | compatibility integration | WPF |
| UO-WPF-003 | WPF compatibility | Legacy local update remains available | Feature flag off | update through adapter layer | adapter returns feature disabled, no server side effect | local path remains authoritative until cutover | compatibility integration | WPF |
