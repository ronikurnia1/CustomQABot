
@description('Language project name.')
param languageProjectName string
param location string = resourceGroup().location
@description('Solution name.')
param appName string

@allowed([ 'F0', 'S' ])
param languageSku string
@allowed([ 'free', 'basic' ])
param searchSku string
@allowed([ 'F1', 'B1' ])
param appServicePlanSku string
@allowed([ 'F0', 'S1' ])
param azureBotSku string

param repositoryUrl string = 'https://github.com/ronikurnia1/CustomQABot.git'
param branch string = 'main'

var searchName = toLower('${appName}-search')
var botServiceName = toLower('${appName}-service')
var appServicePlanName = toLower('${appName}-ServicePlan')
var webSiteName = toLower('${appName}-webapp')
var storageAccountName = toLower('${appName}StorageAccount')

resource searchService 'Microsoft.Search/searchServices@2022-09-01' = {
    location: location
    name: searchName
    sku: {
        name: searchSku
    }
}

resource manageIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
    name: '${appName}-identity'
    location: location
}

resource languageService 'Microsoft.CognitiveServices/accounts@2022-12-01' = {
    location: location
    name: toLower('${appName}-language')
    kind: 'TextAnalytics'
    sku: {
        name: languageSku
    }
    properties: {
        apiProperties: {
            qnaAzureSearchEndpointId: searchService.id
            qnaAzureSearchEndpointKey: searchService.listAdminKeys().primaryKey
        }
    }
    
    identity: {
        type: 'SystemAssigned'
    }
}

resource botService 'Microsoft.BotService/botServices@2022-09-15' = {
    location: 'global'
    name: botServiceName
    kind: 'azurebot'
    sku: {
        name: azureBotSku
    }
    properties: {
        displayName: botServiceName
        msaAppMSIResourceId: manageIdentity.id
        msaAppId: manageIdentity.properties.clientId
        msaAppTenantId: manageIdentity.properties.tenantId
        msaAppType: 'UserAssignedMSI'
        endpoint: 'https://${appService.properties.defaultHostName}/api/messages'
        schemaTransformationVersion: '1.3'
        disableLocalAuth: false
        isStreamingSupported: false
        publishingCredentials: null
    }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
    name: appServicePlanName
    location: location
    sku: {
        name: appServicePlanSku
    }
    properties: {
        reserved: true
    }
}

resource appService 'Microsoft.Web/sites@2022-03-01' = {
    name: webSiteName
    location: location
    kind: 'linux'
    identity: {
        type: 'UserAssigned'
        userAssignedIdentities: {
            '${manageIdentity.id}': {}
        }
    }
    properties: {
        serverFarmId: appServicePlan.id
        siteConfig: {
            linuxFxVersion: 'DOTNETCORE|7.0'
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
                    value: languageService.properties.endpoint
                }
                {
                    name: 'LanguageEndpointKey'
                    value: languageService.listKeys().key1
                }
                {
                    name: 'ProjectName'
                    value: languageProjectName
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

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
    name: storageAccountName
    location: location
    sku: {
        name: 'Standard_LRS'
    }
    kind: 'StorageV2'
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
