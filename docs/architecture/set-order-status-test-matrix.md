# SetOrderStatus Test Matrix

| Test ID | Category | Scenario | Preconditions | Request | Expected response | Authoritative state | Automation | Client relevance |
|---|---|---|---|---|---|---|---|---|
| ORD-STS-001 | Canonical status | successful allowed transition | draft order exists | `POST /api/orders/{id}/status` `ACCEPTED` | `200`, `STATUS_CHANGED` | `orders.status = ACCEPTED` | integration | WPF |
| ORD-STS-002 | Validation | unknown order id | order absent | status request | `404`, `ORDER_NOT_FOUND` | no writes | integration | WPF |
| ORD-STS-003 | Validation | invalid status | existing order | `status=WRONG` | `400`, `INVALID_STATUS` | status unchanged | integration | WPF |
| ORD-STS-004 | Transition rule | shipped target forbidden | draft order | `status=SHIPPED` | `400`, `ORDER_STATUS_SHIPPED_FORBIDDEN` | status unchanged | integration | WPF |
| ORD-STS-005 | Transition rule | draft target forbidden | draft order | `status=DRAFT` | `400`, `ORDER_STATUS_INVALID_TARGET` | status unchanged | integration | WPF |
| ORD-STS-006 | Transition rule | shipped order cannot be changed | order already shipped | `status=ACCEPTED` | `400`, `ORDER_STATUS_CHANGE_FORBIDDEN` | status remains shipped | integration | WPF |
| ORD-STS-007 | Guarantees | no docs and no ledger writes | existing snapshots | successful status change | success | docs unchanged, ledger unchanged | integration | WPF |
| ORD-STS-008 | WPF compatibility | feature flag routes to API | flag enabled | status-only save via WPF service | success | status updated | adapter/integration | WPF |
| ORD-STS-009 | WPF compatibility | forbidden transition surfaces validation | flag enabled + forbidden case | WPF service call | validation failure | status unchanged | adapter/integration | WPF |
| ORD-STS-010 | WPF compatibility | legacy fallback remains available | flag disabled | WPF service call | `FeatureDisabled` | no server write | adapter/integration | WPF |
