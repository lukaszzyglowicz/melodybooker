@description('SQL logical server name (globally unique)')
param serverName string

@description('SQL database name')
param databaseName string

@description('Azure region')
param location string

@description('Azure AD admin object id (the deploying user or group) with full SQL admin rights')
param aadAdminObjectId string

@description('Azure AD admin login name (UPN or group display name)')
param aadAdminLogin string

@description('Azure AD tenant id')
param aadTenantId string

@description('Database SKU, e.g. Basic, S0')
param skuName string = 'Basic'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    // Azure AD-only authentication: no SQL login/password exists at all (see infrastructure.md
    // risk register — avoids the IP-allowlist / credential-rotation trap).
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: aadAdminLogin
      sid: aadAdminObjectId
      tenantId: aadTenantId
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: skuName
    tier: skuName == 'Basic' ? 'Basic' : 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

// Allow Azure services (App Service outbound) to reach the server without per-IP allowlisting.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output serverName string = sqlServer.name
output databaseName string = sqlDatabase.name
