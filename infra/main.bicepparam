using 'main.bicep'

param resourceGroupName = 'rg-safari-explorer'
param location = 'westeurope'
param webAppName = 'safari-ai-explorer'
param storageAccountName = 'safariaiexplorer'
param appServiceSku = 'B1'

param googleMapsApiKey = readEnvironmentVariable('GOOGLE_MAPS_API_KEY', '')
param dataUploaderPrincipalId = readEnvironmentVariable('AZURE_DATA_UPLOADER_ID', '')
