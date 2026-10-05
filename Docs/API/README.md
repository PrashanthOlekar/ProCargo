# API

Base path `/api/v1`. JSON (camelCase), enums accepted as names or numbers and returned as names. Swagger UI is at
`/swagger` when `Swagger:Enabled` is true (on in Development and in the dev/test Azure environments).

## Conventions

| Topic | Rule |
| --- | --- |
| Success | The resource itself, a `PagedResult<T>` (`items`, `pageNumber`, `pageSize`, `totalRecords`, `totalPages`), `{ id, number }` for creates, or `{ success, message }` for commands |
| Errors | Always `{ success: false, message, errorCode, errors?, traceId }`; `errors` maps field names to messages |
| Status codes | 400 validation, 401 not signed in, 403 permission or ownership, 404 not found (also for records the caller may not see), 409 conflict or stale `rowVersion`, 422 business rule, 429 rate limited, 502 provider failure |
| Paging | `pageNumber` (1-based), `pageSize` (max 100), `search`, whitelisted `sortBy` / `sortDirection` |
| Dates | Instants are UTC ISO-8601 (`…Z`); report periods are IST calendar dates (`yyyy-MM-dd`, inclusive) |
| Concurrency | Records that can be edited carry `rowVersion` (base64); send it back on update, 409 if someone changed it first |
| Idempotency | `POST /payments` and `POST /payments/offline` honour an `Idempotency-Key` header; a retry returns the original result |
| Files | Multipart upload, 10 MB per file, PDF/JPEG/PNG/WebP checked by content signature; downloads stream through the API |

## Rate limits (per user when signed in, otherwise per IP)

| Policy | Limit | Endpoints |
| --- | --- | --- |
| global | 300 / minute | everything |
| auth | 10 / minute per IP | login, register, refresh, forgot/reset password |
| otp | 6 / 5 minutes | pickup and delivery OTP send/verify |
| public | 20 / minute per IP | contact form, price estimate |
| webhook | 120 / minute per IP | payment gateway webhooks |

## Endpoint catalogue

| Area | Endpoints |
| --- | --- |
| Auth `/auth` | `register`, `login`, `refresh`, `logout`, `forgot-password`, `reset-password`, `change-password`, `GET me` |
| Users `/users` | list, get, create staff (invite e-mail), update, `roles`, `activate`, `deactivate`, `unlock` |
| Roles `/roles` | list, `permissions`, get, create, update, `permissions` (PUT), delete (non-system, unused) |
| Master data `/master-data` | `reference` (public cache bundle), states, cities, vehicle-types, goods-types, document-types (GET/POST/PUT) |
| Settings `/settings` | list, `PUT {key}` |
| Pricing `/pricing` | `estimate` (public), `configuration`, CRUD for vehicle-rates, distance-slabs, additional-charges, rules, tax-rates, commission-rules |
| Public `/public` | `contact` (honeypot-protected) |
| Customers `/customers` | list, `me`, get, update, activate/deactivate, addresses, contacts, documents |
| Owners `/owners` | list, `me`, get, update, `business`, addresses, bank-accounts (add, deactivate, verify, reveal), verify, activate/deactivate |
| Drivers `/drivers` | list, `me`, `available`, get, create, update, `license`, `availability`, verify, activate/deactivate |
| Vehicles `/vehicles` | list, `available`, `expiring`, get, create, update, `availability`, verify, activate/deactivate |
| Documents `/documents/{entityType}/{entityId}` | list, upload, `{id}/file`, `{id}/verify`, delete |
| Bookings `/bookings` | list, get, create (draft), update, `submit`, `review`, `hold`, `resume`, `reject`, `cancel`, `history`, `notes` |
| Quotations `/quotations` | list, get, `preview`, create, update, `send`, `withdraw`, `accept`, `reject`, `history` |
| Trips `/trips` | list, get, create (assign), `reassign-vehicle`, `reassign-driver`, `history`, `assignments`, `pickup/otp`, `pickup/verify`, `start`, `delivery/otp`, `delivery/verify`, `hold`, `resume`, `exception`, `exception/resolve`, `cancel`, `locations`, `tracking`, `proof-of-delivery` (+ files) |
| Invoices `/invoices` | list, get, create from delivered booking, `adjustments`, `cancel` |
| Payments `/payments` | list, get, start online payment, `confirm`, `sandbox/complete` (non-production), `webhooks/{gateway}`, `offline`, `refunds`, `reconciliation` |
| Settlements `/settlements` | list, `eligible-trips`, `summary` (owner), get, create, `adjustments`, `approve`, `process`, `complete`, `fail`, `cancel` |
| Support | `/support/tickets` (+ comments), `/support/complaints` (+ assign, status), `/support/enquiries` (+ handled) |
| Notifications `/notifications` | mine, `unread-count`, `read`, `read-all`, `templates` (GET/PUT) |
| Dashboards `/dashboard` | `operations`, `customer`, `owner`, `driver` |
| Reports `/reports` | `bookings-by-day`, `bookings-by-status`, `revenue`, `trip-performance`, `settlements`, `top-customers`, `partner-summary`, `{report}/export` (CSV) |
| Audit `/audit-logs` | list with entity, user, action and date filters |
| Health | `/health`, `/health/ready` (not under `/api/v1`) |

Each endpoint's permission is listed on its controller action and in [Authorization](../Authorization/README.md).

## Payment webhooks

`POST /api/v1/payments/webhooks/razorpay` is anonymous but rejects any request whose `X-Razorpay-Signature` is not a
valid HMAC-SHA256 of the raw body with the webhook secret. Processing is idempotent on the gateway payment id, so
redeliveries are harmless. A browser-side "payment succeeded" callback (`confirm`) is only accepted after the checkout
signature is verified, and the amount always comes from the server's own order record, never from the client.
