using System.Net;
using System.Text.Json;
using ApimMcpDemo.Functions.Services;
using ApimMcpDemo.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ApimMcpDemo.Functions.Functions;

/// <summary>
/// MCP Tools: check_stock, restock_product
/// Provides inventory stock-level checks and restock order creation.
/// </summary>
public class InventoryFunctions
{
    private readonly InMemoryDataStore _store;
    private readonly ILogger<InventoryFunctions> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public InventoryFunctions(InMemoryDataStore store, ILogger<InventoryFunctions> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// MCP Tool: check_stock
    /// Returns the current stock level for a product including available quantity,
    /// reorder threshold, and warehouse location.
    /// </summary>
    [Function("GetProductStock")]
    public async Task<HttpResponseData> GetProductStock(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/products/{id}/stock")] HttpRequestData req,
        string id)
    {
        try
        {
            _logger.LogInformation("GetProductStock called: productId={ProductId}", id);

            var product = _store.GetProduct(id);
            if (product is null)
            {
                return await NotFoundAsync(req, $"Product '{id}' not found.");
            }

            var stock = _store.GetStockLevel(id);
            if (stock is null)
            {
                return await NotFoundAsync(req, $"Stock information for product '{id}' not available.");
            }

            var response = new ApiResponse<StockLevel>
            {
                Success = true,
                Data = stock,
                TotalCount = 1
            };

            return await OkAsync(req, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetProductStock for id={Id}", id);
            return await ErrorAsync(req, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// MCP Tool: restock_product
    /// Creates a pending restock order for a product that is low on inventory.
    /// Validates that the product exists and that the requested quantity is positive.
    /// </summary>
    [Function("CreateRestockOrder")]
    public async Task<HttpResponseData> CreateRestockOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/orders/restock")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("CreateRestockOrder called");

            RestockOrder? order;
            try
            {
                order = await JsonSerializer.DeserializeAsync<RestockOrder>(
                    req.Body, JsonOptions);
            }
            catch (JsonException)
            {
                return await BadRequestAsync(req, "Request body is not valid JSON.");
            }

            if (order is null || string.IsNullOrWhiteSpace(order.ProductId))
            {
                return await BadRequestAsync(req, "productId is required.");
            }

            if (order.Quantity <= 0)
            {
                return await BadRequestAsync(req, "quantity must be greater than 0.");
            }

            if (string.IsNullOrWhiteSpace(order.RequestedBy))
            {
                return await BadRequestAsync(req, "requestedBy is required.");
            }

            var product = _store.GetProduct(order.ProductId);
            if (product is null)
            {
                return await NotFoundAsync(req, $"Product '{order.ProductId}' not found.");
            }

            var result = new RestockOrderResult
            {
                OrderId = Guid.NewGuid().ToString(),
                ProductId = order.ProductId,
                Quantity = order.Quantity,
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            };

            _store.AddRestockOrder(result);

            _logger.LogInformation(
                "Restock order created: orderId={OrderId}, productId={ProductId}, qty={Qty}",
                result.OrderId, result.ProductId, result.Quantity);

            var apiResponse = new ApiResponse<RestockOrderResult>
            {
                Success = true,
                Data = result,
                TotalCount = 1,
                Message = $"Restock order created successfully for product '{product.Name}'."
            };

            var httpResponse = req.CreateResponse(HttpStatusCode.Created);
            httpResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await httpResponse.WriteStringAsync(JsonSerializer.Serialize(apiResponse, JsonOptions));
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateRestockOrder");
            return await ErrorAsync(req, "An unexpected error occurred.");
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<HttpResponseData> OkAsync<T>(HttpRequestData req, T body)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(body, JsonOptions));
        return response;
    }

    private static async Task<HttpResponseData> BadRequestAsync(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.BadRequest);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(
            new ApiResponse<object> { Success = false, Message = message }, JsonOptions));
        return response;
    }

    private static async Task<HttpResponseData> NotFoundAsync(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.NotFound);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(
            new ApiResponse<object> { Success = false, Message = message }, JsonOptions));
        return response;
    }

    private static async Task<HttpResponseData> ErrorAsync(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.InternalServerError);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(
            new ApiResponse<object> { Success = false, Message = message }, JsonOptions));
        return response;
    }
}
