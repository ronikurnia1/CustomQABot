param appName string
param location string = resourceGroup().location
param searchSku string = 'basic'
param languageSku string = 'S'
var searchName = toLower('${appName}-search')

resource searchService 'Microsoft.Search/searchServices@2022-09-01' = {
    location: location
    name: searchName
    sku: {
        name: searchSku
    }
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

output searchServiceName string = searchService.name