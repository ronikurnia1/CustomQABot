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
param teamsWebhook string

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

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
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
        msaAppMSIResourceId: managedIdentity.id
        msaAppId: managedIdentity.properties.clientId
        msaAppTenantId: managedIdentity.properties.tenantId
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

resource commService 'Microsoft.Communication/communicationServices@2022-07-01-preview' = {
    name: toLower('${appName}-comm-svc')
    location: 'global'
    properties: {
        dataLocation: 'unitedstates'
        linkedDomains: [
            commServiceEmailDomain.id
        ]
    }
}

resource commServiceEmail 'Microsoft.Communication/emailServices@2022-07-01-preview' = {
    name: toLower('${appName}-comm-email')
    location: 'global'
    properties: {
        dataLocation: 'unitedstates'
    }
}

resource commServiceEmailDomain 'Microsoft.Communication/emailServices/domains@2022-07-01-preview' = {
    name: toLower('${appName}-emaildomain')
    location: 'global'
    parent: commServiceEmail
    properties: {
        domainManagement: 'AzureManagedDomain'
        userEngagementTracking: 'Enabled'
        validSenderUsernames: {
             '@DoNotReply': 'DoNotReply'
        }
    }
}

resource appService 'Microsoft.Web/sites@2022-03-01' = {
    name: webSiteName
    location: location
    kind: 'linux'
    identity: {
        type: 'UserAssigned'
        userAssignedIdentities: {
            '${managedIdentity.id}': {}
        }
    }
    properties: {
        serverFarmId: appServicePlan.id
        siteConfig: {
            linuxFxVersion: 'DOTNETCORE|7.0'
            appSettings: [
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
                    value: managedIdentity.properties.clientId
                }
                {
                    name: 'MicrosoftAppTenantId'
                    value: managedIdentity.properties.tenantId
                }
                {
                    name: 'MicrosoftAppType'
                    value: 'UserAssignedMSI'
                }
                {
                    name: 'CommunicationServiceConnectionString'
                    value: commService.listKeys().primaryConnectionString
                }
                {
                    name: 'TeamsWebHook'
                    value: teamsWebhook
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
