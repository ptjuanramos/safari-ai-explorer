targetScope = 'subscription'

@description('Resource group that holds every resource; created if it doesn\'t exist.')
param resourceGroupName string

@description('Region of the resource group and every resource in it.')
param location string

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

resource group 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module resources 'resources.bicep' = {
  name: 'safari-resources'
  scope: group
  params: {
    location: location
    webAppName: webAppName
    storageAccountName: storageAccountName
    appServiceSku: appServiceSku
    linuxFxVersion: linuxFxVersion
    googleMapsApiKey: googleMapsApiKey
    dataUploaderPrincipalId: dataUploaderPrincipalId
    dataUploaderPrincipalType: dataUploaderPrincipalType
  }
}

output resourceGroupName string = group.name
output webAppName string = resources.outputs.webAppName
output webAppUrl string = resources.outputs.webAppUrl
output storageAccountName string = resources.outputs.storageAccountName
output storageAccountUrl string = resources.outputs.storageAccountUrl
