# DeleteOrder Test Matrix

| Test ID | Category | Scenario | Preconditions | Request | Expected response | Authoritative state | Automation | Client relevance |
|---|---|---|---|---|---|---|---|---|
| DEL-ORD-001 | Canonical delete | successful delete of allowed order | draft order without docs/shipments | `DELETE /api/orders/{id}` | `200`, `result=DELETED` | order removed, lines removed | integration | WPF, future API clients |
| DEL-ORD-002 | Validation | unknown order id | order absent | `DELETE /api/orders/999` | `404`, `ORDER_NOT_FOUND` | no writes | integration | WPF |
| DEL-ORD-003 | Validation | non-draft order forbidden | order status != `DRAFT` | delete request | `400`, `ORDER_DELETE_FORBIDDEN_STATUS` | order still exists | integration | WPF |
| DEL-ORD-004 | Validation | customer order with outbound docs forbidden | order linked to outbound docs | delete request | `400`, `ORDER_HAS_OUTBOUND_DOCS` | order still exists | integration | WPF |
| DEL-ORD-005 | Validation | customer order with shipments forbidden | shipped totals > 0 | delete request | `400`, `ORDER_HAS_SHIPMENTS` | order still exists | integration | WPF |
| DEL-ORD-006 | Validation | internal order with production docs forbidden | internal order + production docs | delete request | `400`, `ORDER_HAS_PRODUCTION_DOCS` | order still exists | integration | WPF |
| DEL-ORD-007 | Validation | internal order with receipt progress forbidden | internal order + qty received > 0 | delete request | `400`, `ORDER_HAS_PRODUCTION_RECEIPTS` | order still exists | integration | WPF |
| DEL-ORD-008 | State guarantees | delete writes no docs and no ledger | existing docs/ledger snapshot | delete request | success | docs unchanged, ledger unchanged | integration | WPF |
| DEL-ORD-009 | WPF compatibility | feature flag routes delete to API | WPF settings flag enabled | delete through `WpfDeleteOrderService` | success | order removed | adapter/integration | WPF |
| DEL-ORD-010 | WPF compatibility | forbidden delete surfaces validation | flag enabled + forbidden scenario | delete through WPF service | validation failure | order still exists | adapter/integration | WPF |
| DEL-ORD-011 | WPF compatibility | legacy fallback stays available | flag disabled | delete through WPF service | `FeatureDisabled` | no server write | adapter/integration | WPF |
