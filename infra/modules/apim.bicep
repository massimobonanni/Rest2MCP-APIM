/*
  Azure API Management module
  - Creates APIM instance with SystemAssigned Managed Identity
  - Populates Named Values used by the inbound policy
  - Connects to Application Insights for diagnostics
  
  Recommended SKUs:
    Developer   — no SLA, ~30 min provisioning, suitable for demos
    StandardV2  — 99.95% SLA, production workloads, semantic caching
*/
targetScope = 'resourceGroup'

@description('Name of the APIM instance.')
param name string

@description('Azure region.')
param location string = resourceGroup().location

@description('Resource tags.')
param tags object = {}

@description('APIM publisher organisation name.')
param publisherName string

@description('APIM publisher email address.')
param publisherEmail string

@description('APIM SKU. Developer for demos; StandardV2 for production.')
@allowed(['Developer', 'StandardV2'])
param skuName string = 'Developer'

@description('Backend URL for the Function App — stored as a Named Value.')
param functionAppUrl string

@description('Entra ID tenant ID for JWT validation — stored as a Named Value.')
param tenantId string

@description('Application Insights instrumentation key for APIM logger.')
param applicationInsightsInstrumentationKey string

@description('Optional: Event Hub connection string for usage logging. Leave empty to skip.')
@secure()
param eventHubConnectionString string = ''

// ---------------------------------------------------------------------------
// APIM instance
// ---------------------------------------------------------------------------

resource apim 'Microsoft.ApiManagement/service@2023-09-01-preview' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: skuName
    capacity: 1
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    publisherName: publisherName
    publisherEmail: publisherEmail
    customProperties: {
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls10': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls11': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Ssl30': 'false'
    }
  }
}

// ---------------------------------------------------------------------------
// Application Insights logger
// ---------------------------------------------------------------------------

resource apimLogger 'Microsoft.ApiManagement/service/loggers@2023-09-01-preview' = {
  parent: apim
  name: 'appinsights-logger'
  properties: {
    loggerType: 'applicationInsights'
    credentials: {
      instrumentationKey: applicationInsightsInstrumentationKey
    }
    isBuffered: true
    resourceId: ''  // connectionString-based logger
    description: 'Application Insights logger for APIM'
  }
}

// ---------------------------------------------------------------------------
// APIM Diagnostics — connect to Application Insights
// ---------------------------------------------------------------------------

resource apimDiagnostics 'Microsoft.ApiManagement/service/diagnostics@2023-09-01-preview' = {
  parent: apim
  name: 'applicationinsights'
  properties: {
    alwaysLog: 'allErrors'
    loggerId: apimLogger.id
    sampling: {
      samplingType: 'fixed'
      percentage: 100
    }
    frontend: {
      request: {
        headers: []
        body: { bytes: 0 }
      }
      response: {
        headers: []
        body: { bytes: 0 }
      }
    }
    backend: {
      request: {
        headers: []
        body: { bytes: 0 }
      }
      response: {
        headers: []
        body: { bytes: 0 }
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Named Values — used by the inbound policy ({{name}} syntax)
// ---------------------------------------------------------------------------

resource nvTenantId 'Microsoft.ApiManagement/service/namedValues@2023-09-01-preview' = {
  parent: apim
  name: 'tenant-id'
  properties: {
    displayName: 'tenant-id'
    value: tenantId
    secret: false
  }
}

resource nvFunctionsBaseUrl 'Microsoft.ApiManagement/service/namedValues@2023-09-01-preview' = {
  parent: apim
  name: 'functions-base-url'
  properties: {
    displayName: 'functions-base-url'
    value: functionAppUrl
    secret: false
  }
}

resource nvApimHost 'Microsoft.ApiManagement/service/namedValues@2023-09-01-preview' = {
  parent: apim
  name: 'apim-host'
  properties: {
    displayName: 'apim-host'
    // APIM gateway hostname follows a known pattern — set at creation time
    value: '${name}.azure-api.net'
    secret: false
  }
}

resource nvEventHubConnection 'Microsoft.ApiManagement/service/namedValues@2023-09-01-preview' = if (!empty(eventHubConnectionString)) {
  parent: apim
  name: 'eventhub-connection'
  properties: {
    displayName: 'eventhub-connection'
    value: eventHubConnectionString
    secret: true  // Connection strings are always secret
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

output name string = apim.name
output id string = apim.id
output principalId string = apim.identity.principalId
output gatewayUrl string = apim.properties.gatewayUrl
output gatewayHostname string = '${name}.azure-api.net'
