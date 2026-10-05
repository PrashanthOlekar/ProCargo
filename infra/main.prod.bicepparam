using './main.bicep'

// Production. Replace the two SQL admin values with your Entra group; nothing secret belongs in this file.
param environmentName = 'prod'
param location = 'centralindia'
param sqlAdminObjectId = '00000000-0000-0000-0000-000000000000'
param sqlAdminLogin = 'procargo-sql-admins'
param webPortalUrl = 'https://www.procargo.com'
param operationsPortalUrl = 'https://operations.procargo.com'
