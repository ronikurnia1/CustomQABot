param languageProjectName string = 'uob-qa'
param location string = resourceGroup().location
param appName string = 'qaservice'

module cognitive 'CognitiveService.bicep' = {
    name: 'cognitiveService'
    params: {
        appName: appName
        location: location
    }
}

module bot 'BotService.bicep' = {
    name: 'appService'
    params: {
        languageEndpointHostName: cognitive.outputs.languageEndpointHostName
        languageEndpointKey: cognitive.outputs.languageEnpointKey
        languageProjectName: languageProjectName
        appName: appName
        location: location
    }
    dependsOn: [
        cognitive
    ]
}