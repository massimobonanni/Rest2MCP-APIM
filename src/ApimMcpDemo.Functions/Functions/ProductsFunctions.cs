using System.Net;
using System.Text.Json;
using ApimMcpDemo.Core.Entities;
using ApimMcpDemo.Core.Interfaces;
using ApimMcpDemo.RestModels;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ApimMcpDemo.Functions.Functions;

/// <summary>
/// MCP Tool: search_products, get_product
/// Provides product catalog search and retrieval operations.
/// </summary>
public class ProductsFunctions
{
    private readonly IDataStore _store;
    private readonly ILogger<ProductsFunctions> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ProductsFunctions(IDataStore store, ILogger<ProductsFunctions> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// MCP Tool: search_products
    /// Search the product catalog by optional keyword and/or category.
    /// Returns a list of matching products with pricing and SKU.
    /// </summary>
    [Function("SearchProducts")]
    public async Task<HttpResponseData> SearchProducts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/products")] HttpRequestData req)
    {
        try
        {
            var query = req.Query["q"];
            var category = req.Query["category"];

            _logger.LogInformation("SearchProducts called: q={Query}, category={Category}", query, category);

            var products = _store.SearchProducts(query, category);

            var response = new ApiResponse<List<Product>>
            {
                Success = true,
                Data = products,
                TotalCount = products.Count
            };

            return await OkAsync(req, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchProducts");
            return await ErrorAsync(req, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// MCP Tool: get_product
    /// Returns detailed information about a specific product by its unique ID.
    /// </summary>
    [Function("GetProduct")]
    public async Task<HttpResponseData> GetProduct(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/products/{id}")] HttpRequestData req,
        string id)
    {
        try
        {
            _logger.LogInformation("GetProduct called: id={Id}", id);

            var product = _store.GetProduct(id);
            if (product is null)
            {
                return await NotFoundAsync(req, $"Product '{id}' not found.");
            }

            var response = new ApiResponse<Product>
            {
                Success = true,
                Data = product,
                TotalCount = 1
            };

            return await OkAsync(req, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetProduct for id={Id}", id);
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
