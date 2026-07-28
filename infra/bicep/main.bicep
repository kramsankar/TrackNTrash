// =====================================================================================
// TrackNTrash — Module 13: main deployment (subscription-agnostic, resource-group scope).
// Deploy per environment with the matching parameter file (dev/uat/prod).
//   az deployment group create -g rg-tracktrash-dev -f main.bicep -p @params.dev.json
// =====================================================================================
targetScope = 'resourceGroup'

@description('Environment: dev | uat | prod')
@allowed(['dev', 'uat', 'prod'])
param env string

@description('Azure region')
param location string = resourceGroup().location

@description('Region for Azure SQL (some regions gate new SQL servers; override when needed)')
param sqlLocation string = location

@description('Base name; resources are suffixed with env.')
param baseName string = 'tracktrash'

@description('SQL admin login')
param sqlAdminLogin string
@secure()
@description('SQL admin password')
param sqlAdminPassword string

@description('Object id of the ops principal granted Key Vault secret access')
param opsGroupObjectId string

@description('Principal type for the ops Key Vault role assignment')
@allowed(['Group', 'User', 'ServicePrincipal'])
param opsPrincipalType string = 'Group'

var suffix = '${baseName}-${env}'
var tags = { system: 'TrackNTrash', env: env }

// Deterministic per-resource-group token so globally-unique resource names don't collide.
var uniq = uniqueString(resourceGroup().id)
var uniq6 = take(uniq, 6)

// ---------------- Observability ----------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${suffix}'
  location: location
  tags: tags
  properties: { sku: { name: 'PerGB2018' }, retentionInDays: env == 'prod' ? 90 : 30 }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${suffix}'
  location: location
  tags: tags
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: logAnalytics.id }
}

// ---------------- Key Vault ----------------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: take('kv-${baseName}${env}-${uniq6}', 24)
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    enablePurgeProtection: env == 'prod' ? true : null
  }
}

// ---------------- Storage (frames / photos) ----------------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: take(replace(toLower('st${uniq}${env}'), '-', ''), 24)
  location: location
  tags: tags
  sku: { name: env == 'prod' ? 'Standard_ZRS' : 'Standard_LRS' }
  kind: 'StorageV2'
  properties: { allowBlobPublicAccess: false, minimumTlsVersion: 'TLS1_2' }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource exceptionsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'exceptions'
}
resource passSamplesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'pass-samples'
}
resource podContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'pod'
}

// Lifecycle: exceptions 1 year, pass-samples 30 days.
resource lifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'expire-pass-samples'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: { blobTypes: ['blockBlob'], prefixMatch: ['pass-samples/'] }
            actions: { baseBlob: { delete: { daysAfterModificationGreaterThan: 30 } } }
          }
        }
        {
          name: 'expire-exceptions'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: { blobTypes: ['blockBlob'], prefixMatch: ['exceptions/'] }
            actions: { baseBlob: { delete: { daysAfterModificationGreaterThan: 365 } } }
          }
        }
      ]
    }
  }
}

// ---------------- Azure SQL ----------------
// SQL server name is derived from the SQL region so switching regions never collides with a
// stale record from a prior region-gated attempt.
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: 'sql-${suffix}-${take(uniqueString(resourceGroup().id, sqlLocation), 6)}'
  location: sqlLocation
  tags: tags
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'TrackNTrash'
  location: sqlLocation
  tags: tags
  sku: env == 'prod' ? { name: 'S3', tier: 'Standard' } : { name: 'S1', tier: 'Standard' }
  properties: { collation: 'SQL_Latin1_General_CP1_CI_AS' }
}

resource sqlAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

// ---------------- Service Bus ----------------
resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: 'sb-${suffix}-${uniq6}'
  location: location
  tags: tags
  sku: { name: env == 'prod' ? 'Standard' : 'Standard', tier: 'Standard' }
}
resource sbExceptionsTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBus
  name: 'exceptions'
}
resource sbTrackingEventsTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBus
  name: 'tracking-events'
}
resource sbFnoQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBus
  name: 'fno-business-events'
}
resource sbRepairQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBus
  name: 'd365-repair'
}

// ---------------- IoT Hub ----------------
resource iotHub 'Microsoft.Devices/IotHubs@2023-06-30' = {
  name: 'iot-${suffix}-${uniq6}'
  location: location
  tags: tags
  sku: { name: env == 'prod' ? 'S1' : 'S1', capacity: 1 }
}

// ---------------- App Service (Tracking API) ----------------
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-${suffix}'
  location: location
  tags: tags
  sku: env == 'prod' ? { name: 'P1v3', tier: 'PremiumV3' } : { name: 'B1', tier: 'Basic' }
  properties: { reserved: false }
}

resource trackingApi 'Microsoft.Web/sites@2023-12-01' = {
  name: 'app-tracking-${suffix}-${uniq6}'
  location: location
  tags: tags
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'ConnectionStrings__TrackNTrash', value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/sql-connection/)' }
        { name: 'ServiceBus__ConnectionString', value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/servicebus-connection/)' }
        { name: 'ServiceBus__Topic', value: 'exceptions' }
      ]
    }
  }
}

// ---------------- Function Apps (tracking ingest, d365 integration, asset metrics) ----------------
resource funcStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: take(replace(toLower('stf${uniq}${env}'), '-', ''), 24)
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: { allowBlobPublicAccess: false, minimumTlsVersion: 'TLS1_2' }
}

resource funcPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'planfunc-${suffix}'
  location: location
  tags: tags
  sku: { name: 'Y1', tier: 'Dynamic' }
  properties: {}
}

resource trackingFunc 'Microsoft.Web/sites@2023-12-01' = {
  name: 'func-tracking-${suffix}-${uniq6}'
  location: location
  tags: tags
  kind: 'functionapp'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: funcPlan.id
    httpsOnly: true
    siteConfig: {
      appSettings: [
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${funcStorage.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${funcStorage.listKeys().keys[0].value}' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'IoTHubEventHub', value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/iot-eventhub/)' }
        { name: 'DockEventHubName', value: 'dock-verifications' }
      ]
    }
  }
}

// Grant the API + Function managed identities Key Vault secret read + Storage blob access.
var kvSecretsUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
var blobContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')

resource apiKvRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, trackingApi.id, 'kvsecrets')
  scope: keyVault
  properties: { roleDefinitionId: kvSecretsUser, principalId: trackingApi.identity.principalId, principalType: 'ServicePrincipal' }
}
resource funcKvRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, trackingFunc.id, 'kvsecrets')
  scope: keyVault
  properties: { roleDefinitionId: kvSecretsUser, principalId: trackingFunc.identity.principalId, principalType: 'ServicePrincipal' }
}
resource funcBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, trackingFunc.id, 'blob')
  scope: storage
  properties: { roleDefinitionId: blobContributor, principalId: trackingFunc.identity.principalId, principalType: 'ServicePrincipal' }
}
resource opsKvRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, opsGroupObjectId, 'kvsecrets')
  scope: keyVault
  properties: { roleDefinitionId: kvSecretsUser, principalId: opsGroupObjectId, principalType: opsPrincipalType }
}

output trackingApiUrl string = 'https://${trackingApi.properties.defaultHostName}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output iotHubName string = iotHub.name
output storageAccount string = storage.name
