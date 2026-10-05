@description('Environment name, used as a naming suffix (dev/prod)')
@allowed(['dev', 'prod'])
param environment string = 'dev'

@description('Azure region for all resources')
param location string = 'westeurope'

@description('Azure region for the SQL server (may differ if primary region has capacity restrictions)')
param sqlLocation string = location

@description('Base name used to derive resource names, e.g. melodybooker')
param baseName string = 'melodybooker'

@description('App Service Plan SKU (Basic B1 has no deployment slots; Standard S1 does)')
param appServicePlanSku string = 'B1'

@description('Azure SQL Database SKU (Basic DTU recommended for MVP; avoid Serverless, see risk register)')
param sqlSkuName string = 'Basic'

@description('Azure AD object id of the admin user/group for Azure SQL (AAD-only auth, no SQL password)')
param aadAdminObjectId string

@description('Azure AD admin login (UPN or group display name) for Azure SQL')
param aadAdminLogin string

@description('Azure AD tenant id')
param aadTenantId string = tenant().tenantId

var suffix = '${baseName}-${environment}'
var sqlServerName = toLower('${suffix}-sql-${uniqueString(resourceGroup().id, sqlLocation)}')
var keyVaultName = toLower(take('${suffix}-kv-${uniqueString(resourceGroup().id)}', 24))
var webAppName = toLower('${suffix}-app-${uniqueString(resourceGroup().id)}')

module appServicePlan 'modules/appServicePlan.bicep' = {
  name: 'appServicePlan'
  params: {
    name: '${suffix}-plan'
    location: location
    skuName: appServicePlanSku
  }
}

module appInsights 'modules/appInsights.bicep' = {
  name: 'appInsights'
  params: {
    namePrefix: suffix
    location: location
  }
}

module sqlDatabase 'modules/sqlDatabase.bicep' = {
  name: 'sqlDatabase'
  params: {
    serverName: sqlServerName
    databaseName: '${baseName}db'
    location: sqlLocation
    aadAdminObjectId: aadAdminObjectId
    aadAdminLogin: aadAdminLogin
    aadTenantId: aadTenantId
    skuName: sqlSkuName
  }
}

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    name: keyVaultName
    location: location
    tenantId: aadTenantId
  }
}

module webApp 'modules/webApp.bicep' = {
  name: 'webApp'
  params: {
    name: webAppName
    location: location
    appServicePlanId: appServicePlan.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    keyVaultUri: keyVault.outputs.uri
    alwaysOn: appServicePlanSku != 'F1'
  }
}

// --- Key Vault access (RBAC) ---

var keyVaultSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource keyVaultRef 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// Deploying user gets Secrets Officer so this same deployment can write the connection-string secret.
resource deployerSecretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVaultRef.id, aadAdminObjectId, keyVaultSecretsOfficerRoleId)
  scope: keyVaultRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsOfficerRoleId)
    principalId: aadAdminObjectId
    principalType: 'User'
  }
}

// Web App's system-assigned identity gets read-only Secrets User so it can resolve Key Vault references.
resource webAppSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVaultRef.id, webAppName, keyVaultSecretsUserRoleId)
  scope: keyVaultRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: webApp.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

// Connection string uses Active Directory Managed Identity auth — no password anywhere.
resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVaultRef
  name: 'sql-connection-string'
  properties: {
    value: 'Server=tcp:${sqlDatabase.outputs.serverFqdn},1433;Database=${sqlDatabase.outputs.databaseName};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
  dependsOn: [
    deployerSecretsOfficer
  ]
}

output webAppName string = webApp.outputs.name
output webAppHostName string = webApp.outputs.defaultHostName
output webAppPrincipalId string = webApp.outputs.principalId
output sqlServerName string = sqlDatabase.outputs.serverName
output sqlServerFqdn string = sqlDatabase.outputs.serverFqdn
output sqlDatabaseName string = sqlDatabase.outputs.databaseName
output keyVaultName string = keyVault.outputs.name
