targetScope = 'resourceGroup'

@description('Region of every resource; defaults to the resource group\'s.')
param location string = resourceGroup().location

@description('Web app name, globally unique: the site is https://<name>.azurewebsites.net.')
@minLength(2)
@maxLength(60)
param webAppName string

@description('Storage account name, globally unique: 3-24 lowercase letters and digits.')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('App Service plan SKU. B1 is the smallest with Always On; F1 is free but sleeps and has a daily CPU quota.')
@allowed(['F1', 'B1', 'B2', 'B3', 'S1', 'P0v3', 'P1v3'])
param appServiceSku string = 'B1'

@description('Runtime stack of the web app (az webapp list-runtimes --os linux).')
param linuxFxVersion string = 'DOTNETCORE|11.0'

@description('Server Google Maps key for /api/directions (Routes and Roads APIs). Empty: directions answer 503.')
@secure()
param googleMapsApiKey string = ''

@description('Object id of a person or group allowed to upload job runs and import saved paths. Empty: nobody.')
param dataUploaderPrincipalId string = ''

@description('principalType of dataUploaderPrincipalId: User, Group or ServicePrincipal.')
@allowed(['User', 'Group', 'ServicePrincipal'])
param dataUploaderPrincipalType string = 'User'

var jobDataContainerName = 'job-data'
var userPathsTableName = 'userpaths'

// Built-in role ids: https://learn.microsoft.com/azure/role-based-access-control/built-in-roles
var roles = {
  blobDataReader: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
  blobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  tableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
}

// ---------------------------------------------------------------------------------------------------------------------
// Storage account
// ---------------------------------------------------------------------------------------------------------------------

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    // Microsoft Entra ID only: the account keys can't be used, so a leaked connection string is worthless.
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource jobData 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: jobDataContainerName
  properties: { publicAccess: 'None' }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource userPaths 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: userPathsTableName
}

// ---------------------------------------------------------------------------------------------------------------------
// App Service
// ---------------------------------------------------------------------------------------------------------------------

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${webAppName}-plan'
  location: location
  kind: 'linux'
  sku: { name: appServiceSku }
  properties: { reserved: true } // reserved = Linux
}

var appSettings = concat(
  [
    { name: 'STORAGE_KIND', value: 'Azure' }
    { name: 'STORAGE_ACCOUNT_URL', value: storage.properties.primaryEndpoints.blob }
  ],
  empty(googleMapsApiKey) ? [] : [{ name: 'GOOGLE_MAPS_API_KEY', value: googleMapsApiKey }]
)

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      alwaysOn: appServiceSku != 'F1'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      appSettings: appSettings
    }
  }
}

resource ftpCredentials 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: webApp
  name: 'ftp'
  properties: { allow: false }
}

resource scmCredentials 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: webApp
  name: 'scm'
  properties: { allow: false }
}

// ---------------------------------------------------------------------------------------------------------------------
// Access (ADR-0004): the app may read the job files but not change them, and may read and write the saved paths.
// ---------------------------------------------------------------------------------------------------------------------

resource appReadsJobData 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(jobData.id, webApp.id, roles.blobDataReader)
  scope: jobData
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.blobDataReader)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource appWritesUserPaths 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(userPaths.id, webApp.id, roles.tableDataContributor)
  scope: userPaths
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.tableDataContributor)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource uploaderWritesJobData 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(dataUploaderPrincipalId)) {
  name: guid(jobData.id, dataUploaderPrincipalId, roles.blobDataContributor)
  scope: jobData
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.blobDataContributor)
    principalId: dataUploaderPrincipalId
    principalType: dataUploaderPrincipalType
  }
}

resource uploaderWritesUserPaths 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(dataUploaderPrincipalId)) {
  name: guid(userPaths.id, dataUploaderPrincipalId, roles.tableDataContributor)
  scope: userPaths
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.tableDataContributor)
    principalId: dataUploaderPrincipalId
    principalType: dataUploaderPrincipalType
  }
}

output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output storageAccountName string = storage.name
output storageAccountUrl string = storage.properties.primaryEndpoints.blob
