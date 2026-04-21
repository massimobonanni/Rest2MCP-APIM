# ApimMcpDemo — Business Operations Assistant

Showcase how **Azure API Management (APIM)** can expose Azure Functions REST APIs as
**MCP (Model Context Protocol)** tools, making them directly consumable by AI agents
(e.g. GitHub Copilot, Azure AI Foundry agents, or any MCP-compatible client).


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

### Step 5 - Add an MCP Server in Azure API Management using the Azure Portal

This step shows how to expose a REST API as an MCP (Model Context Protocol) tool through Azure API Management.

#### Example: Adding the SearchProduct API to the Business-Operation-MCP Server

##### Prerequisites
- An Azure API Management instance already provisioned.
- The **Business-Operation** API already imported in APIM (with at least the `SearchProduct` operation).

---

##### 5.1 – Navigate to your API Management instance

1. Open the [Azure Portal](https://portal.azure.com).
2. In the search bar, type **API Management** and select your instance.

---

##### 5.2 – Open the MCP Servers section

1. In the left-hand menu, under **APIs**, click **MCP Servers**.
2. The list of existing MCP Servers is displayed.

---

##### 5.3 – Create the MCP Server (if it does not exist yet)

1. Click **+ Create** and choose **Expose an API as an MCP server**
2. Fill in the form:
   | Field | Value |
   |---|---|
   | **API** | Choose `Business Operations API` | 
   | **API OPerations** | Select `[GET] Search Products` |
   | **Display name** | `Business Operation MCP` |
   | **Name** | `business-operation-mcp` |
   | **Description** | `MCP Server exposing Business Operation REST APIs as tools` |
3. Click **Create**.

---

##### 5.4 – Verify the MCP Server endpoint

1. Back on the **Business-Operation-MCP** overview page, copy the **MCP Server URL**. It will look like:

```
https://<your-apim-name>.azure-api.net/business-operation-mcp/mcp
```

2. This is the endpoint your MCP-compatible client (e.g., GitHub Copilot, VS Code MCP extension) will connect to.

---

##### 5.6 – Test the MCP Server

1. In VS Code, open **Settings** (`Ctrl+,`) and search for **MCP**.
2. Add the server to your MCP client configuration:
  
```json
{
  "mcpServers": {
    "business-operation-mcp": {
      "type": "http",
      "url": "https://<your-apim-name>.azure-api.net/business-operation-mcp/mcp",
      "headers": {
        "Ocp-Apim-Subscription-Key": "<your-subscription-key>"
      }
    }
  }
}
```

3. Reload the MCP client. The searchProducts tool should now appear in the tool list.

4. Open GitHUb Copilot Chat and write the following prompt:

```
Create a report that contains the total number of products available. Add also a table with the list of products.
```

### Tear down

```bash
azd down
```

---

## Example curl commands

> Replace `$APIM_HOST` with your APIM gateway hostname and `$KEY` with a valid
> Subscription Key in the API MAnagement.

### Search products

```bash
curl "https://$APIM_HOST/api/products?q=keyboard" \
  -H "Ocp-Apim-Subscription-Key: $KEY"
```

### Get a specific product

```bash
curl "https://$APIM_HOST/api/products/prod-002" \
  -H "Ocp-Apim-Subscription-Key: $KEY"
```

### Check stock level

```bash
curl "https://$APIM_HOST/api/products/prod-005/stock" \
  -H "Ocp-Apim-Subscription-Key: $KEY"
```

### Create a restock order

```bash
curl -X POST "https://$APIM_HOST/api/orders/restock" \
  -H "Ocp-Apim-Subscription-Key: $KEY" \
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
  -H "Ocp-Apim-Subscription-Key: $KEY"
```

### Get a specific ticket

```bash
curl "https://$APIM_HOST/api/tickets/tkt-001" \
  -H "Ocp-Apim-Subscription-Key: $KEY"
```

### Create a ticket

```bash
curl -X POST "https://$APIM_HOST/api/tickets" \
  -H "Ocp-Apim-Subscription-Key: $KEY" \
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
  -H "Ocp-Apim-Subscription-Key: $KEY" \
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
  -H "Ocp-Apim-Subscription-Key: $KEY"
```

### Get a KB article

```bash
curl "https://$APIM_HOST/api/kb/kb-001" \
  -H "Ocp-Apim-Subscription-Key: $KEY"
```

### Retrieve the MCP tool manifest

```bash
curl "https://$APIM_HOST/mcp" \
  -H "Ocp-Apim-Subscription-Key: $KEY"
```

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
