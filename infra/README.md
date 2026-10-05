# Azure infrastructure (Bicep)

One template, one resource group per environment.

```bash
az group create -n rg-procargo-prod -l centralindia
az deployment group create -g rg-procargo-prod -f infra/main.bicep -p infra/main.prod.bicepparam
infra/set-secrets.sh <keyVaultName from the outputs>        # once; provider credentials come from env vars
```

Then, once per environment:

1. Deploy the database: run `Database/ProCargo.Database/deploy.sh` (or `deploy.ps1`) against the new server as a
   member of the SQL admin group. Do **not** pass `--with-test-data` outside development.
2. Grant the API's managed identity access: edit and run `infra/grant-api-sql-access.sql` in the `ProCargo` database.
   The identity joins `procargo_app`, which can execute stored procedures and nothing else.
3. Create the first Super Admin: set `Bootstrap__SuperAdminEmail` and `Bootstrap__SuperAdminPassword` as app settings,
   restart, sign in to the operations portal, change the password, then delete both settings.
4. GitHub: set repository variables `AZURE_API_APP_NAME`, `DEPLOY_UI=true`, `DEPLOY_OPERATIONS=true`, secrets
   `AZURE_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID` (OIDC federated credential with Website Contributor on
   the web app), and the Static Web App deployment tokens `AZURE_STATIC_WEB_APPS_API_TOKEN_UI` and
   `AZURE_STATIC_WEB_APPS_API_TOKEN_OPS` (`az staticwebapp secrets list -n <name>`). Set `VITE_API_BASE_URL` for
   the portals to the API URL + `/api/v1` when they are not served behind the same domain.
5. Point the Razorpay webhook at `https://<api>/api/v1/payments/webhooks/razorpay`.

| Resource | Purpose |
| --- | --- |
| App Service (Linux, .NET 10) | ProCargo.API, system-assigned managed identity, health check `/health/ready` |
| Azure SQL (Entra-only auth) | ProCargo database; the API connects with its managed identity, no passwords |
| Storage account | `procargo-files` (KYC documents, POD photos; private) and `dataprotection` (key ring) |
| Key Vault (RBAC, purge protection) | App secrets as Key Vault references; RSA key wrapping the data-protection key ring |
| Application Insights + Log Analytics | Requests, dependencies, Serilog output, App Service logs |
| Static Web Apps ×2 | Customer portal and operations portal, each with its own deployment token |

Shared-key access to storage is disabled; everything authenticates with Entra ID.
