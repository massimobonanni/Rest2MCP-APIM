using System.Net;
using System.Text.Json;
using ApimMcpDemo.Functions.Services;
using ApimMcpDemo.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ApimMcpDemo.Functions.Functions;

/// <summary>
/// MCP Tools: search_knowledge_base, get_kb_article
/// Provides knowledge base search and article retrieval for IT self-service.
/// Always search the knowledge base before creating a support ticket.
/// </summary>
public class KnowledgeBaseFunctions
{
    private readonly InMemoryDataStore _store;
    private readonly ILogger<KnowledgeBaseFunctions> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public KnowledgeBaseFunctions(InMemoryDataStore store, ILogger<KnowledgeBaseFunctions> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// MCP Tool: search_knowledge_base
    /// Searches KB articles using a case-insensitive match across title, summary,
    /// content, and tags. The query parameter is required. Optionally filter by category.
    /// Call this before creating a support ticket to enable self-service resolution.
    /// </summary>
    [Function("SearchKnowledgeBase")]
    public async Task<HttpResponseData> SearchKnowledgeBase(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/kb/search")] HttpRequestData req)
    {
        try
        {
            var query = req.Query["q"];
            var category = req.Query["category"];

            if (string.IsNullOrWhiteSpace(query))
            {
                return await BadRequestAsync(req, "Query parameter 'q' is required.");
            }

            _logger.LogInformation(
                "SearchKnowledgeBase called: q={Query}, category={Category}", query, category);

            var articles = _store.SearchKbArticles(query, category);

            return await OkAsync(req, new ApiResponse<List<KbArticle>>
            {
                Success = true,
                Data = articles,
                TotalCount = articles.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchKnowledgeBase");
            return await ErrorAsync(req, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// MCP Tool: get_kb_article
    /// Returns the full content of a knowledge base article by its unique ID,
    /// including step-by-step instructions and troubleshooting guidance.
    /// </summary>
    [Function("GetKbArticle")]
    public async Task<HttpResponseData> GetKbArticle(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/kb/{id}")] HttpRequestData req,
        string id)
    {
        try
        {
            _logger.LogInformation("GetKbArticle called: id={Id}", id);

            var article = _store.GetKbArticle(id);
            if (article is null)
            {
                return await NotFoundAsync(req, $"KB article '{id}' not found.");
            }

            return await OkAsync(req, new ApiResponse<KbArticle>
            {
                Success = true,
                Data = article,
                TotalCount = 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetKbArticle for id={Id}", id);
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
