# ApimMcpDemo — Business Operations Assistant

Showcase how **Azure API Management (APIM)** can expose Azure Functions REST APIs as
**MCP (Model Context Protocol)** tools, making them directly consumable by AI agents
(e.g. GitHub Copilot, Azure AI Foundry agents, or any MCP-compatible client).

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         AI Agent / Copilot                          │
│              (reads MCP manifest, calls tools via HTTP)             │
└────────────────────────────┬────────────────────────────────────────┘
                             │ HTTPS + Bearer JWT
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                  Azure API Management (APIM)                        │
│                                                                     │
│  GET /mcp          ──► Returns MCP Tool Manifest JSON               │
│                                                                     │
│  Inbound policies:                                                  │
│    ✓ CORS                                                           │
│    ✓ JWT validation (Entra ID)                                      │
│    ✓ Rate limiting (60 req/min)                                     │
│    ✓ Response caching (60s, GET only)                               │
│    ✓ Request tracing / usage logging                                │
│    ✓ Body validation (POST/PATCH)                                   │
│    ✓ Backend URL rewrite                                            │
└────────────────────────────┬────────────────────────────────────────┘
                             │ Forwards to
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│               Azure Functions (Isolated Worker, .NET 8)             │
│                                                                     │
│  ProductsFunctions     GET /api/products                            │
│                        GET /api/products/{id}                       │
│                                                                     │
│  InventoryFunctions    GET /api/products/{id}/stock                 │
│                        POST /api/orders/restock                     │
│                                                                     │
│  TicketsFunctions      GET  /api/tickets                            │
│                        GET  /api/tickets/{id}                       │
│                        POST /api/tickets                            │
│                        PATCH /api/tickets/{id}/status               │
│                                                                     │
│  KnowledgeBaseFunctions  GET /api/kb/search                         │
│                          GET /api/kb/{id}                           │
└─────────────────────────────────────────────────────────────────────┘
```

---

## MCP Tools exposed

| Tool name               | HTTP method & route                     | Description                                  |
|-------------------------|-----------------------------------------|----------------------------------------------|
| `search_products`       | `GET /api/products`                     | Search product catalog by keyword/category   |
| `get_product`           | `GET /api/products/{id}`                | Get a single product by ID                   |
| `check_stock`           | `GET /api/products/{id}/stock`          | Check stock level and warehouse location     |
| `restock_product`       | `POST /api/orders/restock`              | Create a restock order                       |
| `list_tickets`          | `GET /api/tickets`                      | List IT tickets with optional filters        |
| `get_ticket`            | `GET /api/tickets/{id}`                 | Get a single ticket by ID                    |
| `create_ticket`         | `POST /api/tickets`                     | Open a new IT support ticket                 |
| `update_ticket_status`  | `PATCH /api/tickets/{id}/status`        | Update ticket status                         |
| `search_knowledge_base` | `GET /api/kb/search`                    | Search KB articles (search before ticketing) |
| `get_kb_article`        | `GET /api/kb/{id}`                      | Get full KB article content                  |

---

## Prerequisites

| Tool | Version |
|------|---------|
| [.NET 8 SDK](https://dotnet.microsoft.com/download) | 8.0+ |
| [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) | v4 |
| [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (local storage emulator) | latest |
| [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) | latest |
| [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) | latest |
| Azure Subscription | — |

---

## Running locally

### 1. Start Azurite (local Azure Storage emulator)

```bash
azurite --silent --location .azurite --debug .azurite/debug.log
```

Or via VS Code: use the **Azurite** extension and click **Start Azurite**.

### 2. Restore and build

```bash
cd src
dotnet restore ApimMcpDemo.sln
dotnet build ApimMcpDemo.sln
```

### 3. Start the Function App

```bash
cd src/ApimMcpDemo.Functions
func start
```

The runtime will print all registered HTTP endpoints. By default it listens on
`http://localhost:7071`.

---

## Deploying to Azure

The project uses the **Azure Developer CLI (azd)** with the Bicep templates in `infra/`.
A single `azd up` command provisions all resources (Storage, App Insights, Function App,
APIM) and deploys the application code.

### Prerequisites

| Tool | Install |
|------|---------|
| [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) | `winget install Microsoft.Azd` |
| [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) | `winget install Microsoft.AzureCLI` |
| .NET 8 SDK | `winget install Microsoft.DotNet.SDK.8` |

### Step 1 — Authenticate

```bash
azd auth login
```

### Step 2 — Create an environment and set required variables

```bash
azd env new dev
azd env set APIM_PUBLISHER_EMAIL  admin@contoso.com
azd env set APIM_PUBLISHER_NAME   "Contoso"
azd env set APIM_SKU_NAME         Developer    # or StandardV2 for production
```

> `AZURE_ENV_NAME`, `AZURE_LOCATION`, and `AZURE_SUBSCRIPTION_ID` are prompted
> interactively the first time you run `azd up` if not already set.

### Step 3 — Provision infrastructure and deploy

```bash
azd up
```

`azd up` runs in three phases:

1. **Provision** — deploys `infra/main.bicep` at subscription scope, creating:
   - Resource group `rg-<env-name>`
   - Storage Account (Flex Consumption runtime, managed identity)
   - Log Analytics Workspace + Application Insights
   - Azure Functions (Flex Consumption, .NET 8 isolated)
   - Azure API Management with Named Values, API operations, and policies pre-applied
2. **Package** — builds and packages `src/ApimMcpDemo.Functions`
3. **Deploy** — pushes the function package to the Function App

> **Note:** APIM provisioning takes ~30 minutes for the Developer SKU. `azd up` waits
> for completion automatically.

### Step 4 — Verify the deployment

Once `azd up` completes, retrieve the output variables:

```bash
azd env get-values
```

Key outputs:

| Variable | Description |
|---|---|
| `AZURE_APIM_GATEWAY_URL` | APIM gateway base URL |
| `AZURE_FUNCTION_APP_URL` | Function App direct URL |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | App Insights connection string |

Test the MCP manifest endpoint:

```bash
# Read the gateway URL from azd env
APIM_HOST=$(azd env get-values | grep AZURE_APIM_GATEWAY_URL | cut -d= -f2 | tr -d '"')

curl "$APIM_HOST/mcp"
```

### Tear down

```bash
azd down
```

---

## APIM Named Values

All Named Values are populated automatically by the Bicep deployment:

| Name | Value | Secret? |
|------|-------|---------|
| `tenant-id` | Entra ID tenant GUID (from `subscription().tenantId`) | No |
| `functions-base-url` | Function App URL (from Bicep output) | No |
| `apim-host` | APIM gateway hostname (from Bicep) | No |
| `eventhub-connection` | Event Hub connection string (optional, set via `azd env set`) | Yes |

To set the optional Event Hub connection string after deployment:

```bash
azd env set EVENTHUB_CONNECTION_STRING "<your-connection-string>"
azd provision   # re-runs infra only, skips code deployment
```

---

## Example curl commands

> Replace `$APIM_HOST` with your APIM gateway hostname and `$TOKEN` with a valid
> Entra ID Bearer token for the `api://apim-mcp-demo` audience.
> For local testing (no APIM), use `http://localhost:7071` directly (no auth required).

### Search products

```bash
curl "https://$APIM_HOST/api/products?q=keyboard" \
  -H "Authorization: Bearer $TOKEN"
```

### Get a specific product

```bash
curl "https://$APIM_HOST/api/products/prod-002" \
  -H "Authorization: Bearer $TOKEN"
```

### Check stock level

```bash
curl "https://$APIM_HOST/api/products/prod-005/stock" \
  -H "Authorization: Bearer $TOKEN"
```

### Create a restock order

```bash
curl -X POST "https://$APIM_HOST/api/orders/restock" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "prod-005",
    "quantity": 50,
    "requestedBy": "warehouse@company.com",
    "notes": "Stock critically low — urgent order"
  }'
```

### List open tickets

```bash
curl "https://$APIM_HOST/api/tickets?status=open" \
  -H "Authorization: Bearer $TOKEN"
```

### Get a specific ticket

```bash
curl "https://$APIM_HOST/api/tickets/tkt-001" \
  -H "Authorization: Bearer $TOKEN"
```

### Create a ticket

```bash
curl -X POST "https://$APIM_HOST/api/tickets" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Cannot install VS Code on new laptop",
    "description": "Software Portal shows error 403 when trying to install VS Code.",
    "priority": "medium",
    "category": "software",
    "requestedBy": "newuser@company.com"
  }'
```

### Update ticket status

```bash
curl -X PATCH "https://$APIM_HOST/api/tickets/tkt-002/status" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "status": "resolved",
    "notes": "Rolled back KB5034441 update. Laptop performance restored.",
    "updatedBy": "bob.jones@company.com"
  }'
```

### Search knowledge base

```bash
curl "https://$APIM_HOST/api/kb/search?q=vpn+setup" \
  -H "Authorization: Bearer $TOKEN"
```

### Get a KB article

```bash
curl "https://$APIM_HOST/api/kb/kb-001" \
  -H "Authorization: Bearer $TOKEN"
```

### Retrieve the MCP tool manifest

```bash
curl "https://$APIM_HOST/mcp" \
  -H "Authorization: Bearer $TOKEN"
```

---

## Example AI Agent Prompt (MCP Tool Chaining)

Use this prompt to test multi-step tool chaining with an MCP-compatible agent:

> **"We are running low on office supplies. Check our inventory, identify all products
> below their reorder threshold, and create restock orders for each of them."**

**Expected agent behaviour:**

1. Calls `search_products` with `category=Office Supplies` to list all office supply products.
2. Calls `check_stock` for each returned product ID.
3. Identifies products where `quantityAvailable < reorderThreshold`:
   - `prod-005` — HP Toner (4 available, threshold 15)
   - `prod-006` — A4 Paper (7 available, threshold 20)
4. Calls `restock_product` for each understock product with an appropriate quantity.
5. Reports back the created order IDs and confirmation.

---

## Project structure

```
src/
├── ApimMcpDemo.sln
├── ApimMcpDemo.Models/
│   ├── ApimMcpDemo.Models.csproj
│   ├── ApiResponse.cs
│   ├── CreateTicketRequest.cs
│   ├── KbArticle.cs
│   ├── Product.cs
│   ├── RestockOrder.cs
│   ├── RestockOrderResult.cs
│   ├── StockLevel.cs
│   ├── Ticket.cs
│   └── UpdateTicketStatusRequest.cs
├── ApimMcpDemo.Functions/
│   ├── ApimMcpDemo.Functions.csproj
│   ├── Program.cs
│   ├── host.json
│   ├── local.settings.json
│   ├── Services/
│   │   └── InMemoryDataStore.cs
│   └── Functions/
│       ├── ProductsFunctions.cs
│       ├── InventoryFunctions.cs
│       ├── TicketsFunctions.cs
│       └── KnowledgeBaseFunctions.cs
└── APIM/
    └── apim-policy.xml
```

---

## Security notes

- The `local.settings.json` file is excluded from publish but **should not be committed**
  to source control. It is already listed in `.gitignore` by the Functions scaffolding.
- The APIM policy enforces JWT validation on all routes. Bearer tokens must be issued by
  your Entra ID tenant for the `api://apim-mcp-demo` audience.
- For production, replace the `*` CORS origin with your specific allowed origins.
- Named Values marked as **Secret** in APIM are stored encrypted and never returned in
  API responses.
