# Operations portal (ProCargo.Operations)

`OperationsPortal/ProCargo.Operations` — same stack as the customer portal, separate application, separate
deployment, separate refresh cookie (`pc_rt_ops`, 12-hour sessions), `noindex`. Staff sign in here only.

## Screens

| Section | What staff do there |
| --- | --- |
| Dashboard | Work queues with counts that link to filtered lists: bookings to review, quotations awaiting customers, confirmed bookings without a truck, trip exceptions, partners and vehicles to verify, expiring documents, unpaid invoices, settlements to approve, open tickets and complaints |
| Bookings | Review, hold, resume, reject, cancel; internal and customer-visible notes; build a quotation with a live price preview from the pricing tables; assign a trip from vehicles and drivers that are verified, available and big enough; issue the invoice after POD |
| Trips | Status timeline, live route and last location, reassign vehicle or driver before pickup, hold, resume, resolve exceptions, cancel, view proof of delivery |
| Partners | Customers, owners, vehicles, drivers: details, KYC document review (view, verify, reject with reason), entity verification, activate or deactivate, bank-account verification and audited reveal |
| Finance | Invoices (adjust, record offline payment, cancel), payments (detail, refunds, reconciliation by method for a period), settlements (ready-to-settle trips, prepare, adjust, approve, process, complete with bank reference, fail, cancel) |
| Pricing | Rate cards, distance slabs, additional charges, lane and season rules, GST, owner commission — each with effective dates |
| Support | Tickets with replies and internal notes, priority, status, assignment; complaints with investigation and written resolution; website enquiries |
| Reports | Bookings per day and by status, trip performance and on-time rate, revenue, settlements, top customers, partner verification summary; CSV download |
| Administration | Staff users (invite, roles, deactivate, unlock), roles and permission matrix, master data, business settings, message templates, audit log with before/after values |

Menu entries and routes are filtered by the signed-in role's permissions; the API checks them again.

## Daily runbook

1. **Morning**: Dashboard → *Bookings to review* and *Confirmed, no truck yet*; quote and assign.
2. **Through the day**: watch *Trip exceptions*; a driver who cannot verify an OTP can ask for a resend (limited) —
   if the consignee is unreachable, put the trip on hold rather than cancelling.
3. **After delivery**: invoice from the booking once the POD is uploaded; check POD photos first.
4. **Finance**: record offline payments the day they clear; reconcile the previous day under Payments →
   Reconciliation; prepare settlements from *Ready to settle*; a second person approves.
5. **Weekly**: Vehicles → *Expiring in 30 days*; remind owners before documents lapse.

## Run

```bash
cd OperationsPortal/ProCargo.Operations
npm install && npm run dev         # http://localhost:5174
```

Development accounts (password `ProCargo@Dev1`): `admin@`, `ops@`, `finance@`, `support@procargo.test`.
