# ProCargo Logistics

A road-freight platform for India. Customers book loads and get a quotation; ProCargo assigns a verified partner
truck and driver; pickup and delivery are confirmed with OTPs and proof of delivery; the customer is invoiced and pays
online or offline; the truck owner is settled the freight less commission.

| Folder | Solution | What it is |
| --- | --- | --- |
| `Backend/` | `ProCargo.API.sln` | ASP.NET Core 10 Web API (clean architecture, stored procedures only), unit and integration tests |
| `CustomerPortal/` | `ProCargo.UI.sln` | React app: public website, customer, truck-owner and driver portals |
| `OperationsPortal/` | `ProCargo.Operations.sln` | React app for staff: operations, partners, finance, support, administration |
| `Database/` | `ProCargo.Database` | Idempotent SQL Server scripts: schemas, tables, procedures, seed and test data |
| `infra/` | — | Azure Bicep: App Service, Azure SQL, Storage, Key Vault, Application Insights, two Static Web Apps |
| `Docs/` | — | Architecture, API, database, authentication, authorization, workflows, portals, deployment |

The three solutions are independent: each builds, tests and deploys on its own pipeline in `.github/workflows`.

## Quick start (local)

Needs .NET SDK 10, Node.js 22, Docker.

```bash
export MSSQL_SA_PASSWORD='Choose-A-Strong-One-1'
docker compose up -d                                   # SQL Server 2022 + SMTP catcher (http://localhost:5025)
docker compose exec -e SQLCMD=/opt/mssql-tools18/bin/sqlcmd -e SQL_SERVER=localhost -e SQL_USER=sa \
  -e SQL_PASSWORD="$MSSQL_SA_PASSWORD" sql bash /db/ProCargo.Database/deploy.sh --with-test-data

cd Backend/src/ProCargo.API
dotnet user-secrets set "ConnectionStrings:ProCargo" "Server=localhost,1433;Database=ProCargo;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True;Encrypt=True"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 64)"
dotnet user-secrets set "Security:OtpHashingKey" "$(openssl rand -base64 64)"
dotnet user-secrets set "Payments:Sandbox:Secret" "$(openssl rand -base64 32)"
dotnet run --launch-profile https                      # https://localhost:7180/swagger

cd CustomerPortal/ProCargo.UI && npm install && npm run dev            # http://localhost:5173
cd OperationsPortal/ProCargo.Operations && npm install && npm run dev  # http://localhost:5174
```

Sign in with any development account, password `ProCargo@Dev1`:
`customer@`, `owner@`, `driver@procargo.test` on the customer portal;
`admin@`, `ops@`, `finance@`, `support@procargo.test` on the operations portal.
Development uses a sandbox payment gateway and shows trip OTPs on screen, so the whole flow runs without external
accounts. See [Docs/Deployment](Docs/Deployment/README.md) for details and the Azure set-up.

## Tests

```bash
dotnet test Backend/tests/ProCargo.UnitTests
PROCARGO_TEST_CONNECTION="<connection string to a database deployed with --with-test-data>" \
  dotnet test Backend/tests/ProCargo.IntegrationTests       # includes a full booking-to-settlement run
(cd CustomerPortal/ProCargo.UI && npm test)
(cd OperationsPortal/ProCargo.Operations && npm test)
```

CI runs all of these, plus a database deploy-twice check and a SQL smoke test of the workflow.

## Security at a glance

Passwords hashed with PBKDF2; refresh tokens rotated, stored as hashes, sent only in HttpOnly SameSite=Strict
cookies, with reuse detection; OTPs stored as HMACs; bank account numbers and PAN encrypted at rest; per-portal
token audiences; permission policies plus record-level ownership checks; rate limiting; signed payment webhooks;
idempotent payments; audit log with secrets masked; no secrets in the repository (user-secrets locally, Key Vault in
Azure). Details in [Authentication](Docs/Authentication/README.md) and [Authorization](Docs/Authorization/README.md).
