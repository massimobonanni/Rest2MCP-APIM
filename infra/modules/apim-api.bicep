/*
  APIM API module — Business Operations API
  - Creates the API definition for all 10 Function App endpoints
  - Applies API-level policy loaded from infra/policies/api-policy.xml
  - Applies operation-level write policy to POST/PATCH endpoints
  - Adds a virtual /mcp route (handled entirely by the inbound policy)
*/
targetScope = 'resourceGroup'

@description('Name of the existing APIM instance.')
param apimName string

@description('Function App base URL for the APIM backend service.')
param functionAppUrl string

// ---------------------------------------------------------------------------
// Reference existing APIM instance
// ---------------------------------------------------------------------------

resource apim 'Microsoft.ApiManagement/service@2023-09-01-preview' existing = {
  name: apimName
}

// ---------------------------------------------------------------------------
// Load policy XML files at compile time
// ---------------------------------------------------------------------------

var apiPolicyXml = loadTextContent('../policies/api-policy.xml')
var writePolicyXml = loadTextContent('../policies/op-write-policy.xml')

// ---------------------------------------------------------------------------
// API definition
// ---------------------------------------------------------------------------

resource businessOpsApi 'Microsoft.ApiManagement/service/apis@2023-09-01-preview' = {
  parent: apim
  name: 'business-ops-api'
  properties: {
    displayName: 'Business Operations API'
    description: 'REST API exposing product catalog, inventory, IT helpdesk, and knowledge base as MCP tools.'
    subscriptionRequired: true
    protocols: [
      'https'
    ]
    path: ''                  // Empty path — function routes already include full paths
    isCurrent: true
    apiType: 'http'
    serviceUrl: functionAppUrl
  }
}

// ---------------------------------------------------------------------------
// API-level policy
// ---------------------------------------------------------------------------

resource apiPolicy 'Microsoft.ApiManagement/service/apis/policies@2023-09-01-preview' = {
  parent: businessOpsApi
  name: 'policy'
  properties: {
    value: apiPolicyXml
    format: 'xml'
  }
}

// ===========================================================================
// OPERATIONS
// ===========================================================================

// ---------------------------------------------------------------------------
// Products — search_products  GET /api/products
// ---------------------------------------------------------------------------

resource opSearchProducts 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: businessOpsApi
  name: 'search-products'
  properties: {
    displayName: 'Search Products'
    method: 'GET'
    urlTemplate: '/api/products'
    description: 'Search the product catalog by keyword and/or category.'
    request: {
      queryParameters: [
        {
          name: 'q'
          description: 'Search keyword'
          type: 'string'
          required: false
        }
        {
          name: 'category'
          description: 'Filter by category: Electronics, Furniture, Office Supplies'
          type: 'string'
          required: false
        }
      ]
    }
    responses: [
      { statusCode: 200, description: 'List of matching products' }
    ]
  }
}

// ---------------------------------------------------------------------------
// Products — get_product  GET /api/products/{id}
// ---------------------------------------------------------------------------

resource opGetProduct 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: businessOpsApi
  name: 'get-product'
  properties: {
    displayName: 'Get Product'
    method: 'GET'
    urlTemplate: '/api/products/{id}'
    description: 'Get detailed information about a specific product by its ID.'
    templateParameters: [
      {
        name: 'id'
        description: 'The product ID'
        type: 'string'
        required: true
      }
    ]
    responses: [
      { statusCode: 200, description: 'Product details' }
      { statusCode: 404, description: 'Product not found' }
    ]
  }
}

// ---------------------------------------------------------------------------
// Inventory — check_stock  GET /api/products/{id}/stock
// ---------------------------------------------------------------------------

resource opGetProductStock 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: businessOpsApi
  name: 'get-product-stock'
  properties: {
    displayName: 'Check Product Stock'
    method: 'GET'
    urlTemplate: '/api/products/{id}/stock'
    description: 'Check current stock level, reorder threshold, and warehouse location for a product.'
    templateParameters: [
      {
        name: 'id'
        description: 'The product ID'
        type: 'string'
        required: true
      }
    ]
    responses: [
      { statusCode: 200, description: 'Stock level information' }
      { statusCode: 404, description: 'Product not found' }
    ]
  }
}

// ---------------------------------------------------------------------------
// Inventory — restock_product  POST /api/orders/restock
// ---------------------------------------------------------------------------

resource opCreateRestockOrder 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: businessOpsApi
  name: 'create-restock-order'
  properties: {
    displayName: 'Create Restock Order'
    method: 'POST'
    urlTemplate: '/api/orders/restock'
    description: 'Create a pending restock order for a product that is low on inventory.'
    request: {
      representations: [
        {
          contentType: 'application/json'
          examples: {
            default: {
              value: {
                productId: 'prod-005'
                quantity: 50
                requestedBy: 'warehouse@company.com'
                notes: 'Urgent — stock critically low'
              }
            }
          }
        }
      ]
    }
    responses: [
      { statusCode: 201, description: 'Restock order created' }
      { statusCode: 400, description: 'Invalid request' }
      { statusCode: 404, description: 'Product not found' }
    ]
  }
}

resource opCreateRestockOrderPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2023-09-01-preview' = {
  parent: opCreateRestockOrder
  name: 'policy'
  properties: {
    value: writePolicyXml
    format: 'xml'
  }
}

// ---------------------------------------------------------------------------
// Tickets — list_tickets  GET /api/tickets
// ---------------------------------------------------------------------------

resource opListTickets 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: businessOpsApi
  name: 'list-tickets'
  properties: {
    displayName: 'List Tickets'
    method: 'GET'
    urlTemplate: '/api/tickets'
    description: 'List IT support tickets with optional filters for status, priority, and assignee.'
    request: {
      queryParameters: [
        {
          name: 'status'
          description: 'Filter by status: open, in-progress, resolved, closed'
          type: 'string'
          required: false
        }
        {
          name: 'priority'
          description: 'Filter by priority: low, medium, high, critical'
          type: 'string'
          required: false
        }
        {
          name: 'assignee'
          description: 'Filter by assigned team member email'
          type: 'string'
          required: false
        }
      ]
    }
    responses: [
      { statusCode: 200, description: 'List of tickets' }
    ]
  }
}

// ---------------------------------------------------------------------------
// Tickets — get_ticket  GET /api/tickets/{id}
// ---------------------------------------------------------------------------

resource opGetTicket 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: businessOpsApi
  name: 'get-ticket'
  properties: {
    displayName: 'Get Ticket'
    method: 'GET'
    urlTemplate: '/api/tickets/{id}'
    description: 'Get full details of a specific support ticket by its ID.'
    templateParameters: [
      {
        name: 'id'
        description: 'The ticket ID'
        type: 'string'
        required: true
      }
    ]
    responses: [
      { statusCode: 200, description: 'Ticket details' }
      { statusCode: 404, description: 'Ticket not found' }
    ]
  }
}

// ---------------------------------------------------------------------------
// Tickets — create_ticket  POST /api/tickets
// ---------------------------------------------------------------------------

resource opCreateTicket 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: businessOpsApi
  name: 'create-ticket'
  properties: {
    displayName: 'Create Ticket'
    method: 'POST'
    urlTemplate: '/api/tickets'
    description: 'Create a new IT support ticket. Always search the knowledge base first.'
    request: {
      representations: [
        {
          contentType: 'application/json'
          examples: {
            default: {
              value: {
                title: 'Cannot connect to VPN'
                description: 'Error 800 when connecting from home.'
                priority: 'high'
                category: 'network'
                requestedBy: 'user@company.com'
              }
            }
          }
        }
      ]
    }
    responses: [
      { statusCode: 201, description: 'Ticket created' }
      { statusCode: 400, description: 'Invalid request' }
    ]
  }
}

resource opCreateTicketPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2023-09-01-preview' = {
  parent: opCreateTicket
  name: 'policy'
  properties: {
    value: writePolicyXml
    format: 'xml'
  }
}

// ---------------------------------------------------------------------------
// Tickets — update_ticket_status  PATCH /api/tickets/{id}/status
// ---------------------------------------------------------------------------

resource opUpdateTicketStatus 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: businessOpsApi
  name: 'update-ticket-status'
  properties: {
    displayName: 'Update Ticket Status'
    method: 'PATCH'
    urlTemplate: '/api/tickets/{id}/status'
    description: 'Update the status of an existing support ticket. Setting status to resolved records the resolution time.'
    templateParameters: [
      {
        name: 'id'
        description: 'The ticket ID'
        type: 'string'
        required: true
      }
    ]
    request: {
      representations: [
        {
          contentType: 'application/json'
          examples: {
            default: {
              value: {
                status: 'resolved'
                notes: 'Issue fixed — rolled back last Windows update.'
                updatedBy: 'support@company.com'
              }
            }
          }
        }
      ]
    }
    responses: [
      { statusCode: 200, description: 'Ticket updated' }
      { statusCode: 404, description: 'Ticket not found' }
    ]
  }
}

resource opUpdateTicketStatusPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2023-09-01-preview' = {
  parent: opUpdateTicketStatus
  name: 'policy'
  properties: {
    value: writePolicyXml
    format: 'xml'
  }
}

// ---------------------------------------------------------------------------
// Knowledge Base — search_knowledge_base  GET /api/kb/search
// ---------------------------------------------------------------------------

resource opSearchKb 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: businessOpsApi
  name: 'search-knowledge-base'
  properties: {
    displayName: 'Search Knowledge Base'
    method: 'GET'
    urlTemplate: '/api/kb/search'
    description: 'Search KB articles by keyword across title, summary, content, and tags. Call before creating a ticket.'
    request: {
      queryParameters: [
        {
          name: 'q'
          description: 'Search query, e.g. "VPN setup", "printer not working"'
          type: 'string'
          required: true
        }
        {
          name: 'category'
          description: 'Optional category filter'
          type: 'string'
          required: false
        }
      ]
    }
    responses: [
      { statusCode: 200, description: 'List of matching KB articles' }
      { statusCode: 400, description: 'Missing q parameter' }
    ]
  }
}

// ---------------------------------------------------------------------------
// Knowledge Base — get_kb_article  GET /api/kb/{id}
// ---------------------------------------------------------------------------

resource opGetKbArticle 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: businessOpsApi
  name: 'get-kb-article'
  properties: {
    displayName: 'Get KB Article'
    method: 'GET'
    urlTemplate: '/api/kb/{id}'
    description: 'Retrieve the full content of a knowledge base article by its ID.'
    templateParameters: [
      {
        name: 'id'
        description: 'The KB article ID'
        type: 'string'
        required: true
      }
    ]
    responses: [
      { statusCode: 200, description: 'KB article with full content' }
      { statusCode: 404, description: 'Article not found' }
    ]
  }
}

// ---------------------------------------------------------------------------
// MCP Definition
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Business Operation MCP
// ---------------------------------------------------------------------------
/*
resource businessOpsMcp 'Microsoft.ApiManagement/service/apis@2025-03-01-preview' = {
  parent: apim
  name: 'business-ops-mcp'
  properties: {
    type: 'mcp'
    isCurrent: true
    apiRevision: '1'
    subscriptionRequired: false
    path: 'business-operation-mcp'
    protocols: [
      'https'
    ]
    authenticationSettings: {
      oAuth2AuthenticationSettings: []
      openidAuthenticationSettings: []
    }
    subscriptionKeyParameterNames: {
      header: 'Ocp-Apim-Subscription-Key'
      query: 'subscription-key'
    }
    displayName: 'Business Operations MCP'
    description: 'MCP definition for Business Operations tools, including product catalog, inventory, IT helpdesk, and knowledge base.'
  }
}
*/

// ---------------------------------------------------------------------------
// MCP Tool — Search Products
// ---------------------------------------------------------------------------
/*
resource mcpToolSearchProducts 'Microsoft.ApiManagement/service/apis/tools@2025-03-01-preview' = {
  parent: businessOpsMcp
  name: 'search-products'
  properties: {
    displayName: 'searchProducts'
    description: 'Search the product catalog by keyword and/or category.'
    operationId: opSearchProducts.id
  }
}
*/

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

output apiId string = businessOpsApi.id
output apiName string = businessOpsApi.name
