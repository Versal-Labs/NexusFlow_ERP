targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short application name used in resource names.')
param appName string = 'nexusflow'

@description('Unique NexusFlow instance id. Used for tenant-style blob container names.')
param instanceId string = appName

@description('Linux container image, e.g. myregistry.azurecr.io/nexusflow-erp:1.0.0.')
param containerImage string

@description('SQL administrator login for provisioning. Prefer Entra ID administration for production.')
param sqlAdminLogin string

@secure()
@description('SQL administrator password. Not used by the app when managed identity connection strings are selected.')
param sqlAdminPassword string

@description('SQL database name.')
param sqlDatabaseName string = 'NexusFlow'

@description('App Service Plan SKU.')
param appServicePlanSku string = 'P1v3'

var suffix = uniqueString(resourceGroup().id, appName, instanceId)
var normalizedInstance = toLower(replace(replace(replace(instanceId, '_', '-'), '.', '-'), ' ', '-'))
var storageAccountName = toLower(take('${replace(appName, '-', '')}${suffix}', 24))
var sqlServerName = toLower('${appName}-sql-${suffix}')
var planName = '${appName}-plan-${suffix}'
var webAppName = '${appName}-web-${suffix}'
var keyVaultName = toLower(take('${appName}-kv-${suffix}', 24))
var tenantContainerName = take('tenant-${normalizedInstance}', 63)
var managedIdentityConnectionString = 'Server=tcp:${sqlServerName}.database.windows.net,1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True;'
var keyVaultSecretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
var storageBlobDataContributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  name: 'default'
  parent: storage
}

resource tenantContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: tenantContainerName
  parent: blobService
  properties: {
    publicAccess: 'None'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlFirewallAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  name: 'AllowAzureServices'
  parent: sqlServer
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  name: sqlDatabaseName
  parent: sqlServer
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  sku: {
    name: appServicePlanSku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerImage}'
      alwaysOn: true
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'NEXUSFLOW_INSTANCE_ID'
          value: instanceId
        }
        {
          name: 'NEXUSFLOW_INSTANCE_ROOT'
          value: '/home/nexusflow/state'
        }
        {
          name: 'NEXUSFLOW_DEPLOYMENT_PROFILE'
          value: 'AzureAppService'
        }
        {
          name: 'NEXUSFLOW_SECRET_STORE'
          value: 'Environment'
        }
        {
          name: 'NEXUSFLOW_STATE_STORE'
          value: 'AzureBlob'
        }
        {
          name: 'NEXUSFLOW_DATA_PROTECTION_STORE'
          value: 'AzureBlob'
        }
        {
          name: 'NEXUSFLOW_STORAGE_MODE'
          value: 'AzureBlob'
        }
        {
          name: 'NEXUSFLOW_STORAGE_CONTAINER'
          value: tenantContainerName
        }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: managedIdentityConnectionString
          type: 'SQLAzure'
        }
        {
          name: 'AzureBlobStorage'
          connectionString: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storage.listKeys().keys[0].value}'
          type: 'Custom'
        }
      ]
    }
  }
}

resource webAppKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.identity.principalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource webAppBlobAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, webApp.identity.principalId, storageBlobDataContributorRoleId)
  scope: storage
  properties: {
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageBlobDataContributorRoleId
  }
}

output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDb.name
output storageAccountName string = storage.name
output tenantContainer string = tenantContainer.name
output keyVaultName string = keyVault.name
output postProvisioningNote string = 'Create a database user for the App Service managed identity before running /install, or replace DefaultConnection with a SQL auth/Key Vault reference connection string.'
