using './main.bicep'

// Shared development / test environment: smaller SKUs, Sandbox payments, Swagger on.
param environmentName = 'dev'
param location = 'centralindia'
param sqlAdminObjectId = '00000000-0000-0000-0000-000000000000'
param sqlAdminLogin = 'procargo-sql-admins'
