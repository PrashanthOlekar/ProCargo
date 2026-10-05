# Workflows

## End to end

```mermaid
sequenceDiagram
  autonumber
  actor C as Customer
  actor O as Operations
  actor D as Driver
  actor F as Finance
  C->>C: Create draft booking (route, pickup window, goods, contacts)
  C->>O: Submit booking
  O->>O: Review → build quotation from pricing tables (discount above limit needs approval)
  O->>C: Send quotation (valid until expiry; expiry job marks it Expired)
  C->>O: Accept quotation → booking Confirmed
  O->>D: Create trip: verified, available vehicle + driver of the right type and capacity
  D->>C: Pickup OTP sent to the consignor contact → driver verifies → start trip
  D->>O: Live locations while in transit
  D->>C: Delivery OTP sent to the consignee contact → driver verifies → upload POD (photos, receiver)
  F->>C: Generate invoice from the accepted quotation (trip becomes Completed)
  C->>F: Pay online (Razorpay) or Finance records an offline payment
  F->>F: Settlement for the owner: freight − commission (± adjustments) → approve (different user) → process → paid out
```

Every transition is checked against the maps in `ProCargo.Domain/DomainRules/StatusRules.cs`, written to the
entity's status-history table with who and when, audited, and announced through notification templates (in-app,
e-mail and, for OTPs, SMS). The stored procedure also receives the expected current status, so two people acting on
the same record at once get a 409 instead of a double transition.

## Booking

| From | Allowed next |
| --- | --- |
| Draft | Submitted, Cancelled |
| Submitted | Under review, Rejected, Cancelled, On hold |
| Under review | Quoted, Rejected, Cancelled, On hold |
| Quoted | Confirmed (quotation accepted), Under review (quotation rejected or expired), Cancelled, On hold |
| Confirmed | Truck assigned, Cancelled, On hold |
| Truck assigned | In transit, Confirmed (trip cancelled), Cancelled |
| In transit | Delivered |
| Delivered | Invoiced |
| Invoiced | Paid, Delivered (invoice cancelled) |
| Paid | Closed |
| On hold | back to the status it was held from, or Cancelled |

Customers may edit a booking while it is Draft or Submitted and cancel it until pickup.

## Quotation

Draft → Sent → Accepted / Rejected / Expired; Draft or Sent → Withdrawn. Only bookings that are Submitted, Under
review or Quoted can be quoted; sending a new version supersedes the previous open one, and customers never see
drafts. The price is computed by `PricingCalculator` from the effective rate card,
distance slabs, additional charges (loading, unloading, waiting, night, special handling), lane or seasonal rules and
GST; staff can add manual charges and a discount. Accepted quotations are immutable and are what the invoice bills.

## Trip

| From | Allowed next |
| --- | --- |
| Scheduled | Picked up (pickup OTP verified), Cancelled, On hold |
| Picked up | In transit (driver starts), On hold, Exception |
| In transit | Delivered (delivery OTP verified), On hold, Exception |
| Delivered | POD uploaded |
| POD uploaded | Completed (invoice issued) |
| Completed | Closed (owner settled), POD uploaded (invoice cancelled) |
| On hold | Scheduled, Picked up, In transit, Cancelled |
| Exception | In transit, Picked up, On hold, Cancelled |

Vehicle and driver can be reassigned before pickup; each assignment is kept in `TripAssignment`. Vehicle and driver
availability follow the trip (On a trip while active, Available afterwards).

## Finance

* **Invoice**: one per delivered booking with POD; lines copied from the accepted quotation; due date from payment
  terms. Adjustments are audited lines (credit or debit) with a reason. Unpaid invoices can be cancelled.
* **Payment**: online payments create a gateway order first; the payment becomes Paid only from a verified checkout
  signature or a signed webhook. Partial payments move the invoice to Partly paid. Offline payments (bank transfer,
  UPI, cheque, cash) are recorded by Finance with a reference. Refunds are partial or full, never more than captured.
* **Settlement**: available once the trip is Completed and its invoice is Paid. Amount = freight before tax −
  commission (rule by vehicle type, with a minimum) ± adjustments. Pending approval → Approved (by someone other
  than the preparer) → Payout in progress → Paid out (bank reference recorded) or Payout failed; Pending or Approved
  can be cancelled.

## Partner onboarding and verification

Owners register in the customer portal, add business details, bank accounts, vehicles, drivers and KYC documents
(PAN, GST, RC, insurance, permit, fitness, PUC, driving licence). Operations reviews each document and each
entity: Not verified → Under review → Verified or Rejected. A vehicle or driver is assignable only while every
mandatory document is verified and unexpired, so a lapsed insurance or licence takes it out of the assignment lists
(the vehicles screen has an "expiring in 30 days" view). Bank accounts are verified separately by Operations.

## Support

Tickets: Open → In progress / Waiting on customer → Resolved → Closed (Resolved can reopen to In progress); staff can
add internal notes the requester never sees. Complaints (Delay, Damage, Behaviour, Billing, Payment, Other) can be
linked to a booking or trip: Open → Assigned → Investigating → Resolved or Rejected (with a written resolution) →
Closed.
