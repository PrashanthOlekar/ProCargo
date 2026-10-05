# Database

SQL Server 2022 / Azure SQL. The schema is owned by `Database/ProCargo.Database`, deployed by `deploy.sh`
(`deploy.ps1` on Windows). Every script is idempotent (`IF NOT EXISTS`, `CREATE OR ALTER`, `MERGE`), so the same
command creates a new database or upgrades an existing one; CI deploys twice to prove it.

## Layout

| Folder | Contents |
| --- | --- |
| `01-Database` | Creates the database (skipped if it exists) and sets options (snapshot isolation, RCSI) |
| `02-Schemas` | `sec`, `mst`, `core`, `fin`, `sup`, `aud`, `rpt` |
| `03-Tables` | Tables in dependency order, plus sequences for human-readable numbers |
| `04-Constraints` | Foreign keys, check constraints |
| `05-Indexes` | Covering and filtered indexes for list screens and lookups |
| `06-Views` | Reporting views in `rpt` |
| `07-Functions` | Inline helpers (number formatting, IST date boundaries) |
| `08-StoredProcedures` | All reads and writes used by the API |
| `09-SeedData` | Lookups, roles and permissions, reference data (states, cities, vehicle and goods types), pricing and message templates |
| `10-TestData` | Development accounts and sample bookings (only with `--with-test-data`) |
| `11-Security` | `procargo_app` role: `EXECUTE` on the application schemas, nothing else |
| `tests` | End-to-end smoke test of the booking-to-settlement workflow, run in CI |

## Schemas and main tables

| Schema | Tables |
| --- | --- |
| `sec` | User, Role, Permission, RolePermission, UserRole, RefreshToken, PasswordResetToken, LoginHistory |
| `mst` | Status lookups (booking, trip, quotation, payment, invoice, settlement, refund, verification, availability, ticket, complaint), CustomerType, OwnerType, PaymentMethod, NotificationChannel/Status, State, City, VehicleType, GoodsType, DocumentType, SystemSetting |
| `core` | StoredFile; Customer (+ Address, Contact, Document); VehicleOwner (+ Business, Address, BankAccount, Document); Driver (+ License, Document, AvailabilityHistory); Vehicle (+ Document, AvailabilityHistory); Booking (+ Address, Contact, Item, Note, StatusHistory); Quotation (+ Charge, StatusHistory); Trip (+ Assignment, StatusHistory, LocationHistory, Verification); TrackingProvider; ProofOfDelivery (+ File) |
| `fin` | VehiclePricing, DistancePricing, AdditionalCharge, PricingRule, TaxRate, CommissionRule, Invoice, InvoiceItem, Payment, PaymentAttempt, PaymentRefund, Settlement, SettlementItem |
| `sup` | SupportTicket (+ Comment), Complaint, ContactEnquiry, NotificationTemplate, Notification |
| `aud` | AuditLog |

Conventions: `bigint IDENTITY` keys, `CreatedDateUtc` / `CreatedBy` / `ModifiedDateUtc` / `ModifiedBy` on every
business table, `rowversion` on editable aggregates, soft delete (`IsDeleted`) where history must survive, every
status change written to a `*StatusHistory` table in the same transaction.

## Stored procedure contract

* Naming `schema.usp_Entity_Action` (`core.usp_Vehicle_GetPaged`, `fin.usp_Settlement_ChangeStatus`).
* Parameters are typed; JSON is used only for child collections (booking items, quotation charges) and parsed with
  `OPENJSON … WITH` into typed columns.
* Writes run in `SET XACT_ABORT ON` + explicit transactions and return the new id/number as a result set.
* Expected failures `THROW` codes 50400 (business rule), 50403, 50404, 50409 (duplicate/conflict) or 50412 (stale
  `rowversion`) with a message that is safe to show; the API maps them to HTTP (see [Architecture](../Architecture/README.md)).
* List procedures take `@PageNumber`, `@PageSize`, `@Search`, `@SortBy` (whitelisted in the API) and return
  `COUNT(*) OVER ()` as `TotalRecords` with each row.

## What is never stored

Passwords (only ASP.NET Core Identity PBKDF2 hashes), refresh and reset tokens (SHA-256 hashes), OTPs (HMAC-SHA256
with a server key), full card details (the gateway holds them). Bank account numbers and PAN are encrypted by the API
before they reach the database; only the last four digits are kept in clear for display.

## Deploy

```bash
SQL_SERVER=localhost,1433 SQL_USER=sa SQL_PASSWORD='…' Database/ProCargo.Database/deploy.sh --with-test-data   # local
SQL_SERVER=<server>.database.windows.net SQL_AUTH=ActiveDirectoryDefault Database/ProCargo.Database/deploy.sh      # Azure
```
