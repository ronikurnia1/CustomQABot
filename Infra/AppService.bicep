param appName string = 'ubot'
param sku string = 'S1' // The SKU of App Service Plan
param location string = resourceGroup().location // Location for all resources
param repositoryUrl string = 'https://github.com/Azure-Samples/nodejs-docs-hello-world'
param branch string = 'main'

var appServicePlanName = toLower('${appName}-ServicePlan')
var webSiteName = toLower('${appName}-webapp')
var storageAccountName = toLower('${appName}StorageAccount')

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
    name: appServicePlanName
    location: location
    sku: {
        name: sku
    }
    properties: {
        reserved: true
    }
}

resource appService 'Microsoft.Web/sites@2022-03-01' = {
    name: webSiteName
    location: location
    kind: 'app'
    identity: {
        type: 'UserAssigned'
        userAssignedIdentities: {
            '${manageIdentity.id}': {}
        }
    }
    properties: {
        serverFarmId: appServicePlan.id
        siteConfig: {
            alwaysOn: true
            appSettings: [
                {
                    name: 'DefaultAnswer'
                    value: 'No answer found'
                }
                {
                    name: 'EnablePreciseAnswer'
                    value: 'true'
                }
                {
                    name: 'LanguageEndpointHostName'
                    value: 'https://faq-service.cognitiveservices.azure.com/'
                }
                {
                    name: 'LanguageEndpointKey'
                    value: '3b1aef43b32d40ae8634c051dd75bc50'
                }
                {
                    name: 'ProjectName'
                    value: 'XXXX'
                }
                {
                    name: 'AzureBlobStorageConnectionString'
                    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'                    
                }
                {
                    name: 'MicrosoftAppId'
                    value: manageIdentity.properties.clientId
                }
                {
                    name: 'MicrosoftAppTenantId'
                    value: manageIdentity.properties.tenantId
                }
                {
                    name: 'MicrosoftAppType'
                    value: 'UserAssignedMSI'
                }
            ]
        }
    }
}

resource gitsource 'Microsoft.Web/sites/sourcecontrols@2022-03-01' = {
    parent: appService
    name: 'web'
    properties: {
        repoUrl: repositoryUrl
        branch: branch
        isManualIntegration: true
    }
}

resource manageIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
    name: '${appName}-identity'
    location: location
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
    name: storageAccountName
    location: location
    sku: {
        name: 'Standard_LRS'
    }
    kind: 'StorageV2'
}