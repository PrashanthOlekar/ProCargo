# Authorization

Three layers, all enforced by the API. The portals hide what a role cannot use, but nothing depends on that.

1. **Portal** — the `Staff` policy requires `portal=Operations`, the `External` policy `portal=Web`.
   `PermissionHandler` additionally refuses an internal permission on a Web token and an external permission on an
   Operations token, so a role misconfiguration cannot leak staff powers into the customer site.
2. **Permission** — `[HasPermission(P.X)]` / `[HasAnyPermission(P.X, P.Y)]` on each action, resolved by a dynamic
   policy provider (`perm:X|Y`). Permissions travel in the access token as `perm` claims, so a role change applies at
   the user's next token refresh (at most 15 minutes).
3. **Resource** — services decide whether this caller may touch this record (`AccessGuard`): customers see their own
   bookings, quotations, invoices and payments; owners their own vehicles, drivers, trips and settlements; drivers
   only trips assigned to them. A record that exists but belongs to someone else returns **404**, not 403, so ids
   cannot be probed.

## Roles

Roles are data (`sec.Role`, `sec.RolePermission`) and can be created or changed in the Operations portal by holders
of `ManageRoles`. System roles cannot be deleted; a role in use cannot be deleted.

| Role | Portal | Default permissions |
| --- | --- | --- |
| SuperAdmin | Operations | every internal permission |
| Admin | Operations | every internal permission except ManageRoles and ManageSystemSettings |
| Operations | Operations | partners (view, manage, approve), bookings, quotations, trips (view, assign, update), reports, complaints |
| Finance | Operations | view customers/owners/bookings/trips; invoices, payments, refunds, settlements (create and approve), quotation approval, pricing, reports |
| Support | Operations | view partners, bookings, trips and finance; support tickets, complaints |
| Customer | Web | CreateBookings, RespondToQuotations, MakePayments, RaiseSupportRequests |
| VehicleOwner | Web | ManageOwnFleet, RaiseSupportRequests |
| Driver | Web | PerformTrips, RaiseSupportRequests |

Every staff role also has `AccessOperationsPortal`, which is checked at sign-in.

## Permission catalogue

| Module | Internal permissions |
| --- | --- |
| Security | AccessOperationsPortal, ManageUsers, ManageRoles |
| Customers | ViewCustomers, ManageCustomers |
| Owners / Drivers / Vehicles | View*, Manage*, Approve* for each |
| Bookings | ViewBookings, ManageBookings |
| Quotations | ManageQuotations, ApproveQuotations |
| Trips | ViewTrips, AssignTrips, UpdateTrips |
| Pricing | ManagePricing |
| Finance | ViewFinance, ManageInvoices, ManagePayments, ManageRefunds, ManageSettlements, ApproveSettlements |
| Reports | ViewReports |
| Support | ManageSupport, ManageComplaints |
| Administration | ManageMasterData, ManageSystemSettings, ManageNotificationTemplates, ViewAuditLogs |

The codes are constants in `ProCargo.Domain/Constants/Permissions.cs`, in `09-SeedData/002_RolesPermissions.sql` and
in each portal's `P` map; the three must match.

## Rules that are not just permissions

| Rule | Where |
| --- | --- |
| Settlement maker-checker: whoever prepared a settlement cannot approve it | `SettlementService` |
| Quotation discounts above the configured limit need `ApproveQuotations` | `QuotationService` |
| Refunds cannot exceed the captured amount minus earlier refunds | `PaymentService` + procedure |
| Revealing a full bank account number needs `ManageSettlements` and is audited | `OwnerService` |
| Vehicles and drivers can only be assigned when verified, active, available and with unexpired documents | `TripService` + `VerificationRules` |
| Users cannot deactivate their own account or change their own roles | `UserService` |
| Status changes follow the transition maps in `StatusRules` | every workflow service |
