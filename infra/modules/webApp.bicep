@description('Web App name (globally unique, becomes <name>.azurewebsites.net)')
param name string

@description('Azure region')
param location string

@description('App Service Plan resource id')
param appServicePlanId string

@description('Application Insights connection string')
param appInsightsConnectionString string

@description('Key Vault URI used to build Key Vault reference app settings')
param keyVaultUri string

@description('Enable Always On (requires Basic tier or above)')
param alwaysOn bool = true

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: alwaysOn
      webSocketsEnabled: true
      healthCheckPath: '/health'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/sql-connection-string/)'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'WEBSITE_SWAP_WARMUP_PING_PATH'
          value: '/health'
        }
      ]
    }
  }
}

output principalId string = webApp.identity.principalId
output defaultHostName string = webApp.properties.defaultHostName
output name string = webApp.name
