# ProCargo.Operations — internal staff portal

React 19 + TypeScript + Vite + Material UI, TanStack Query, React Hook Form + Zod. A separate application from the
customer portal: it signs in against the `Operations` portal audience, so only internal (staff) accounts can use it
and staff tokens never work on the customer site.

| Area | Routes | Permission that unlocks it |
| --- | --- | --- |
| Dashboard and work queues | `/` | any staff role |
| Bookings, quotations | `/bookings`, `/quotations` | ViewBookings, ManageQuotations |
| Trips: assignment, reassignment, holds, exceptions, POD | `/trips` | ViewTrips, AssignTrips, UpdateTrips |
| Customers, owners, vehicles, drivers; KYC verification | `/customers`, `/owners`, `/vehicles`, `/drivers` | View*/Approve* |
| Invoices, payments, refunds, reconciliation | `/invoices`, `/payments` | ViewFinance, ManageInvoices, ManagePayments, ManageRefunds |
| Owner settlements (maker-checker) | `/settlements` | ManageSettlements, ApproveSettlements |
| Pricing tables | `/pricing` | ManagePricing |
| Support tickets, complaints, website enquiries | `/support/...` | ManageSupport, ManageComplaints |
| Reports with CSV export | `/reports` | ViewReports (+ ViewFinance for revenue and settlements) |
| Users, roles and permissions | `/users`, `/roles` | ManageUsers, ManageRoles |
| Master data, business settings, message templates | `/settings` | ManageMasterData, ManageSystemSettings, ManageNotificationTemplates |
| Audit log | `/audit` | ViewAuditLogs |

The menu only shows what the signed-in role can use; the API enforces every permission again on each call.

## Run locally

```bash
npm install
cp .env.example .env.local        # adjust VITE_API_PROXY_TARGET if the API runs elsewhere
npm run dev                        # http://localhost:5174
```

Start `Backend/ProCargo.API.sln` first (https://localhost:7180). The dev server proxies `/api` to it.

Development accounts (password `ProCargo@Dev1`): `admin@procargo.test` (Super Admin), `ops@procargo.test`
(Operations Manager), `finance@procargo.test` (Finance), `support@procargo.test` (Support). New staff accounts must
change their password at first sign-in.

## Scripts

`npm run build` (type-check + production bundle), `npm test` (Vitest), `npm run lint`, `npm run typecheck`.

## Deployment

`.github/workflows/operations.yml` builds on every change under `OperationsPortal/` and, when the repository variable
`DEPLOY_OPERATIONS` is `true`, uploads `dist` to its own Azure Static Web App using the secret
`AZURE_STATIC_WEB_APPS_API_TOKEN_OPS`. Set `VITE_API_BASE_URL` at build time when the API is on another origin.
The page sends `noindex` and strict security headers (`public/staticwebapp.config.json`).
