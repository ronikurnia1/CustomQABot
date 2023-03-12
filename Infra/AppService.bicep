param webAppName string = 'bot'
param sku string = 'S1' // The SKU of App Service Plan
param dotNetFxVersion string = 'node|14-lts' // The runtime stack of web app
param location string = resourceGroup().location // Location for all resources
param repositoryUrl string = 'https://github.com/Azure-Samples/nodejs-docs-hello-world'
param branch string = 'main'

var appServicePlanName = toLower('AppServicePlan-${webAppName}')
var webSiteName = toLower('wapp-${webAppName}')


resource appService 'Microsoft.Web/sites@2022-03-01' = {
    name: webAppName
    location: location
    kind: 'app'
    properties: {
         
    }
}