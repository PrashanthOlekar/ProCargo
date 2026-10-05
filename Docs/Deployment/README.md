# Deployment

## Local development

Prerequisites: .NET SDK 10, Node.js 20.19+ (22 recommended), Docker (or a local SQL Server 2022), `sqlcmd`.

```bash
# 1. SQL Server + SMTP catcher
export MSSQL_SA_PASSWORD='Choose-A-Strong-One-1'
docker compose up -d

# 2. Database with development accounts and sample data
docker compose exec -e SQLCMD=/opt/mssql-tools18/bin/sqlcmd -e SQL_SERVER=localhost -e SQL_USER=sa \
  -e SQL_PASSWORD="$MSSQL_SA_PASSWORD" sql bash /db/ProCargo.Database/deploy.sh --with-test-data

# 3. API secrets (stored outside the repo by dotnet user-secrets)
cd Backend/src/ProCargo.API
dotnet user-secrets set "ConnectionStrings:ProCargo" "Server=localhost,1433;Database=ProCargo;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True;Encrypt=True"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 64)"
dotnet user-secrets set "Security:OtpHashingKey" "$(openssl rand -base64 64)"
dotnet user-secrets set "Payments:Sandbox:Secret" "$(openssl rand -base64 32)"
dotnet dev-certs https --trust
dotnet run --launch-profile https            # https://localhost:7180/swagger

# 4. Portals (two terminals)
cd CustomerPortal/ProCargo.UI && npm install && npm run dev                 # http://localhost:5173
cd OperationsPortal/ProCargo.Operations && npm install && npm run dev       # http://localhost:5174
```

Development uses local file storage (`App_Data/files`), the Sandbox payment gateway (a simulated checkout dialog),
SMTP on `localhost:1025` (open http://localhost:5025 to read the e-mails) and returns trip OTPs in the API response
(`Security:ExposeOtpForTesting`), so the whole flow runs without external accounts.

Development accounts, all with password `ProCargo@Dev1`:

| E-mail | Role | Portal |
| --- | --- | --- |
| `admin@procargo.test` | SuperAdmin | Operations |
| `ops@procargo.test` | Operations | Operations |
| `finance@procargo.test` | Finance | Operations |
| `support@procargo.test` | Support | Operations |
| `customer@procargo.test` | Customer | Customer portal |
| `owner@procargo.test` | VehicleOwner | Customer portal |
| `driver@procargo.test` | Driver | Customer portal |

## CI/CD

One workflow per deliverable, each triggered only by changes to its own folder:

| Workflow | Path filter | Steps |
| --- | --- | --- |
| `database.yml` | `Database/**` | Deploy to SQL Server 2022 in Docker, deploy again (idempotency), run the booking-to-settlement smoke test |
| `api.yml` | `Backend/**`, `Database/**` | Restore, build (nullable warnings as errors), unit tests, deploy database, integration tests against it, publish, deploy to App Service (OIDC) |
| `ui.yml` | `CustomerPortal/**` | Install, type-check, lint, unit tests, build, deploy to Static Web Apps |
| `operations.yml` | `OperationsPortal/**` | Same as UI, separate Static Web App and token |
| `infra.yml` | `infra/**` | Build the Bicep template and parameter files |

Deploy jobs run only on `main`, in the `production` GitHub environment (add required reviewers there), and only when
the repository variables that enable them are set (`AZURE_API_APP_NAME`, `DEPLOY_UI`, `DEPLOY_OPERATIONS`).

## Azure

`infra/main.bicep` creates the environment; `infra/README.md` lists the one-time steps (secrets, database
deployment, managed-identity database user, first administrator, GitHub variables).

**Domains matter.** The refresh cookie is `SameSite=Strict`, so the portals and the API must be *same-site*: give them
custom domains under one registrable domain (`www.procargo.com`, `operations.procargo.com`, `api.procargo.com`) and
set the repository variable `API_BASE_URL=https://api.procargo.com/api/v1`, which the portal builds pass as
`VITE_API_BASE_URL`. The default `*.azurestaticapps.net` and
`*.azurewebsites.net` host names are different sites, so sign-in would not survive a page reload there. Update the
`connect-src` of each portal's Content-Security-Policy if the API host name differs from `api.procargo.com`.

## Configuration reference (API)

| Key | Purpose | Where in Azure |
| --- | --- | --- |
| `ConnectionStrings:ProCargo` | SQL connection (managed identity in Azure) | App setting |
| `Jwt:Issuer`, `Jwt:WebAudience`, `Jwt:OperationsAudience` | Token issuer and per-portal audiences | App setting / defaults |
| `Jwt:SigningKey` | HMAC key, ≥ 32 bytes | Key Vault |
| `Jwt:AccessTokenMinutes`, `WebRefreshTokenDays`, `OperationsRefreshTokenHours` | Lifetimes | defaults |
| `Security:OtpHashingKey` | HMAC key for OTPs, ≥ 32 bytes | Key Vault |
| `Security:MaxFailedLoginAttempts`, `LockoutMinutes`, `PasswordResetTokenMinutes`, `InviteTokenMinutes`, `OtpResendSeconds` | Account protection | defaults |
| `Security:ExposeOtpForTesting` | Return OTPs in responses (refused in Production) | `false` |
| `Portals:WebBaseUrl`, `Portals:OperationsBaseUrl` | Links in e-mails | App setting |
| `Cors:AllowedOrigins` | The two portal origins | App setting |
| `FileStorage:Provider` (`Local`/`AzureBlob`), `BlobServiceUri`, `ContainerName` | Documents and POD | App setting |
| `DataProtection:BlobUri`, `DataProtection:KeyVaultKeyId` | Key ring for field encryption | App setting |
| `Email:*` | SMTP host, port, credentials, sender | Key Vault for credentials |
| `Sms:*` | HTTP SMS provider URL, key, sender id (`None` disables) | Key Vault |
| `Payments:Gateway` (`Razorpay`/`Sandbox`), `Payments:Razorpay:*`, `Payments:Sandbox:Secret` | Payments (Sandbox refused in Production) | Key Vault |
| `Bootstrap:SuperAdmin*` | First administrator, remove after use | App setting, temporary |
| `Swagger:Enabled` | Swagger UI | `false` in production |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Telemetry | App setting |
