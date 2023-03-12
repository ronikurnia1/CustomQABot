##Deployment

```dode
$rgName="bot-service"
$location="southeastasia"
az group create --location $location --name $rgName

az deployment group create --resource-group $rgName --template-file main.bicep
```