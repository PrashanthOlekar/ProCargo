// ProCargo Logistics — Azure infrastructure for one environment (dev, test or prod).
//
//   az group create -n rg-procargo-prod -l centralindia
//   az deployment group create -g rg-procargo-prod -f infra/main.bicep -p infra/main.prod.bicepparam
//
// Creates: Log Analytics + Application Insights, Key Vault (RBAC), Storage (documents + data-protection keys),
// Azure SQL (Entra-only auth), App Service plan + API web app (managed identity), and two Static Web Apps
// (customer portal, operations portal). Secrets are never parameters of this template except the ones that must be
// supplied once (see main.*.bicepparam); they land in Key Vault and the app reads them via Key Vault references.

targetScope = 'resourceGroup'

@description('Short environment name used in resource names.')
@allowed(['dev', 'test', 'prod'])
param environmentName string

@description('Primary region. Central India keeps customer data in India.')
param location string = resourceGroup().location

@description('Static Web Apps are only offered in a few regions; East Asia is the closest to India.')
param staticWebAppLocation string = 'eastasia'

@description('Base name; resource names are derived from it plus a short unique suffix.')
@minLength(3)
@maxLength(12)
param baseName string = 'procargo'

@description('Object id of the Entra group (or user) that administers the SQL server.')
param sqlAdminObjectId string

@description('Display name of that Entra group or user.')
param sqlAdminLogin string

@description('App Service plan SKU. P1v3 or higher for production (zone redundancy, deployment slots).')
param appServiceSku string = environmentName == 'prod' ? 'P1v3' : 'B1'

@description('Azure SQL Database SKU name.')
param sqlSkuName string = environmentName == 'prod' ? 'S2' : 'S0'

@description('Custom domain of the customer portal, if any (used for CORS and links in e-mails).')
param webPortalUrl string = ''

@description('Custom domain of the operations portal, if any.')
param operationsPortalUrl string = ''

@description('Payment gateway used by the API. Sandbox is rejected by the API in Production.')
@allowed(['Razorpay', 'Sandbox'])
param paymentGateway string = environmentName == 'prod' ? 'Razorpay' : 'Sandbox'

param tags object = {
  application: 'ProCargo'
  environment: environmentName
}

var suffix = take(uniqueString(resourceGroup().id, baseName, environmentName), 6)
var name = '${baseName}-${environmentName}'
var isProd = environmentName == 'prod'

// ---------------- monitoring ----------------

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${name}-${suffix}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: isProd ? 90 : 30
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${name}-${suffix}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
    DisableLocalAuth: false
  }
}

// ---------------- storage ----------------

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: toLower(take('st${baseName}${environmentName}${suffix}', 24))
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: { name: isProd ? 'Standard_ZRS' : 'Standard_LRS' }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    deleteRetentionPolicy: { enabled: true, days: 30 }
    containerDeleteRetentionPolicy: { enabled: true, days: 30 }
    isVersioningEnabled: isProd
  }
}

resource filesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'procargo-files'
  properties: { publicAccess: 'None' }
}

resource keysContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'dataprotection'
  properties: { publicAccess: 'None' }
}

// ---------------- key vault ----------------

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: take('kv-${name}-${suffix}', 24)
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

// Wraps the ASP.NET Core data-protection key ring (which in turn encrypts bank account numbers and PAN at rest).
resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: vault
  name: 'dataprotection'
  properties: {
    kty: 'RSA'
    keySize: 3072
    keyOps: ['wrapKey', 'unwrapKey']
  }
}

// ---------------- sql ----------------

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: 'sql-${name}-${suffix}'
  location: location
  tags: tags
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: sqlAdminLogin
      sid: sqlAdminObjectId
      tenantId: subscription().tenantId
      principalType: 'Group'
    }
  }
}

resource sqlAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'ProCargo'
  location: location
  tags: tags
  sku: { name: sqlSkuName }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
    requestedBackupStorageRedundancy: isProd ? 'Geo' : 'Local'
  }
}

resource sqlAuditing 'Microsoft.Sql/servers/auditingSettings@2023-08-01-preview' = {
  parent: sqlServer
  name: 'default'
  properties: {
    state: 'Enabled'
    isAzureMonitorTargetEnabled: true
  }
}

// ---------------- static web apps (portals) ----------------

resource webPortal 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-${name}-web-${suffix}'
  location: staticWebAppLocation
  tags: tags
  sku: { name: isProd ? 'Standard' : 'Free', tier: isProd ? 'Standard' : 'Free' }
  properties: {
    allowConfigFileUpdates: true
  }
}

resource operationsPortal 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-${name}-ops-${suffix}'
  location: staticWebAppLocation
  tags: tags
  sku: { name: isProd ? 'Standard' : 'Free', tier: isProd ? 'Standard' : 'Free' }
  properties: {
    allowConfigFileUpdates: true
  }
}

var webUrl = empty(webPortalUrl) ? 'https://${webPortal.properties.defaultHostname}' : webPortalUrl
var opsUrl = empty(operationsPortalUrl) ? 'https://${operationsPortal.properties.defaultHostname}' : operationsPortalUrl

// ---------------- api ----------------

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-${name}-${suffix}'
  location: location
  tags: tags
  kind: 'linux'
  sku: { name: appServiceSku }
  properties: {
    reserved: true
  }
}

var kvRef = 'https://${vault.name}${environment().suffixes.keyvaultDns}/secrets'

resource api 'Microsoft.Web/sites@2023-12-01' = {
  name: 'app-${name}-api-${suffix}'
  location: location
  tags: tags
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: appServiceSku != 'B1' && appServiceSku != 'F1'
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/health/ready'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: isProd ? 'Production' : 'Staging' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
        { name: 'ConnectionStrings__ProCargo', value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${database.name};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connect Timeout=30' }
        { name: 'Jwt__Issuer', value: 'https://app-${name}-api-${suffix}.azurewebsites.net' }
        { name: 'Jwt__SigningKey', value: '@Microsoft.KeyVault(SecretUri=${kvRef}/Jwt--SigningKey)' }
        { name: 'Security__OtpHashingKey', value: '@Microsoft.KeyVault(SecretUri=${kvRef}/Security--OtpHashingKey)' }
        { name: 'Security__ExposeOtpForTesting', value: 'false' }
        { name: 'Portals__WebBaseUrl', value: webUrl }
        { name: 'Portals__OperationsBaseUrl', value: opsUrl }
        { name: 'Cors__AllowedOrigins__0', value: webUrl }
        { name: 'Cors__AllowedOrigins__1', value: opsUrl }
        { name: 'FileStorage__Provider', value: 'AzureBlob' }
        { name: 'FileStorage__BlobServiceUri', value: storage.properties.primaryEndpoints.blob }
        { name: 'FileStorage__ContainerName', value: filesContainer.name }
        { name: 'DataProtection__BlobUri', value: '${storage.properties.primaryEndpoints.blob}${keysContainer.name}/keys.xml' }
        { name: 'DataProtection__KeyVaultKeyId', value: dataProtectionKey.properties.keyUri }
        { name: 'Email__Provider', value: 'Smtp' }
        { name: 'Email__SmtpHost', value: '@Microsoft.KeyVault(SecretUri=${kvRef}/Email--SmtpHost)' }
        { name: 'Email__UserName', value: '@Microsoft.KeyVault(SecretUri=${kvRef}/Email--UserName)' }
        { name: 'Email__Password', value: '@Microsoft.KeyVault(SecretUri=${kvRef}/Email--Password)' }
        { name: 'Sms__Provider', value: isProd ? 'Http' : 'None' }
        { name: 'Sms__ApiUrl', value: '@Microsoft.KeyVault(SecretUri=${kvRef}/Sms--ApiUrl)' }
        { name: 'Sms__ApiKey', value: '@Microsoft.KeyVault(SecretUri=${kvRef}/Sms--ApiKey)' }
        { name: 'Payments__Gateway', value: paymentGateway }
        { name: 'Payments__Razorpay__KeyId', value: '@Microsoft.KeyVault(SecretUri=${kvRef}/Payments--Razorpay--KeyId)' }
        { name: 'Payments__Razorpay__KeySecret', value: '@Microsoft.KeyVault(SecretUri=${kvRef}/Payments--Razorpay--KeySecret)' }
        { name: 'Payments__Razorpay__WebhookSecret', value: '@Microsoft.KeyVault(SecretUri=${kvRef}/Payments--Razorpay--WebhookSecret)' }
        { name: 'Payments__Sandbox__Secret', value: '@Microsoft.KeyVault(SecretUri=${kvRef}/Payments--Sandbox--Secret)' }
        { name: 'Swagger__Enabled', value: isProd ? 'false' : 'true' }
      ]
    }
  }
}

// ---------------- role assignments for the API's managed identity ----------------

var roles = {
  storageBlobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  keyVaultCryptoUser: '12338af0-0e69-4776-bea7-57ae8d297424'
}

resource apiBlobAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, api.id, roles.storageBlobDataContributor)
  scope: storage
  properties: {
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDataContributor)
  }
}

resource apiSecretsAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, api.id, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
  }
}

resource apiCryptoAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, api.id, roles.keyVaultCryptoUser)
  scope: vault
  properties: {
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultCryptoUser)
  }
}

resource apiDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: api
  properties: {
    workspaceId: logs.id
    logs: [
      { category: 'AppServiceHTTPLogs', enabled: true }
      { category: 'AppServiceConsoleLogs', enabled: true }
      { category: 'AppServiceAppLogs', enabled: true }
    ]
    metrics: [{ category: 'AllMetrics', enabled: true }]
  }
}

// ---------------- outputs ----------------

output apiAppName string = api.name
output apiUrl string = 'https://${api.properties.defaultHostName}'
output apiPrincipalId string = api.identity.principalId
output webPortalName string = webPortal.name
output webPortalUrl string = webUrl
output operationsPortalName string = operationsPortal.name
output operationsPortalUrl string = opsUrl
output keyVaultName string = vault.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output storageAccountName string = storage.name
