/*
  main.bicep — AZD entry point (subscription scope)
  Provisions all infrastructure for the ApimMcpDemo solution.

  Resources created:
    - Resource Group
    - Storage Account (Flex Consumption runtime storage)
    - Log Analytics Workspace + Application Insights
    - Azure Functions (Flex Consumption, .NET 8 isolated)
    - Azure API Management (Developer SKU by default)
    - APIM API with 11 operations and policy
*/
targetScope = 'subscription'

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@minLength(1)
@maxLength(64)
@description('Name of the environment. Used to generate unique resource names.')
param environmentName string

@minLength(1)
@description('Azure region for all resources.')
param location string

@description('Publisher organisation name for APIM.')
param apimPublisherName string = 'Contoso'

@description('Publisher email for APIM. Required by the APIM service.')
param apimPublisherEmail string

@description('APIM SKU tier. Use Developer for demos, StandardV2 for production.')
@allowed(['Developer', 'StandardV2'])
param apimSkuName string = 'Developer'

@description('Azure AD / Entra ID tenant ID used for JWT validation in APIM policy.')
param tenantId string = subscription().tenantId

@description('Optional Event Hub connection string for APIM usage logging. Leave empty to skip.')
@secure()
param eventHubConnectionString string = ''

// ---------------------------------------------------------------------------
// Variables
// ---------------------------------------------------------------------------

var resourceSuffix = take(uniqueString(subscription().id, environmentName, location), 6)
var abbrev = take(replace(toLower(environmentName), '-', ''), 10)

var tags = {
  'azd-env-name': environmentName
  environment: environmentName
  project: 'apim-mcp-demo'
}

// Resource names following Azure naming conventions
var storageAccountName = 'st${abbrev}${resourceSuffix}'          // Storage: max 24, no dashes
var logAnalyticsName = 'log-${abbrev}-${resourceSuffix}'
var appInsightsName = 'appi-${abbrev}-${resourceSuffix}'
var functionAppName = 'func-${abbrev}-${resourceSuffix}'
var apimName = 'apim-${abbrev}-${resourceSuffix}'

// ---------------------------------------------------------------------------
// Resource Group
// ---------------------------------------------------------------------------

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

// ---------------------------------------------------------------------------
// Storage Account
// ---------------------------------------------------------------------------

module storage './modules/storage.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    name: storageAccountName
    location: location
    tags: tags
  }
}

// ---------------------------------------------------------------------------
// Monitoring (Log Analytics + Application Insights)
// ---------------------------------------------------------------------------

module monitoring './modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: rg
  params: {
    logAnalyticsName: logAnalyticsName
    applicationInsightsName: appInsightsName
    location: location
    tags: tags
  }
}

// ---------------------------------------------------------------------------
// Azure Functions (Flex Consumption, .NET 8 isolated)
// ---------------------------------------------------------------------------

module functions './modules/functions.bicep' = {
  name: 'functions'
  scope: rg
  params: {
    name: functionAppName
    location: location
    tags: tags
    storageAccountName: storage.outputs.name
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    azdServiceName: 'functions'
  }
}

// ---------------------------------------------------------------------------
// Azure API Management
// ---------------------------------------------------------------------------

module apim './modules/apim.bicep' = {
  name: 'apim'
  scope: rg
  params: {
    name: apimName
    location: location
    tags: tags
    publisherName: apimPublisherName
    publisherEmail: apimPublisherEmail
    skuName: apimSkuName
    functionAppUrl: functions.outputs.functionAppUrl
    tenantId: tenantId
    applicationInsightsInstrumentationKey: monitoring.outputs.applicationInsightsInstrumentationKey
    eventHubConnectionString: eventHubConnectionString
  }
}

// ---------------------------------------------------------------------------
// APIM API — operations + policy
// Deploy after APIM instance is ready
// ---------------------------------------------------------------------------

module apimApi './modules/apim-api.bicep' = {
  name: 'apim-api'
  scope: rg
  params: {
    apimName: apim.outputs.name
    functionAppUrl: functions.outputs.functionAppUrl
  }
}

// ---------------------------------------------------------------------------
// Outputs  (UPPERCASE names are exposed as azd env vars)
// ---------------------------------------------------------------------------

output AZURE_FUNCTION_APP_URL string = functions.outputs.functionAppUrl
output AZURE_APIM_GATEWAY_URL string = apim.outputs.gatewayUrl
