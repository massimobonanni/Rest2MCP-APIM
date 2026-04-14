using System.Net;
using System.Text.Json;
using ApimMcpDemo.Functions.Services;
using ApimMcpDemo.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ApimMcpDemo.Functions.Functions;

/// <summary>
/// MCP Tools: list_tickets, get_ticket, create_ticket, update_ticket_status
/// Provides full lifecycle management for IT support tickets.
/// </summary>
public class TicketsFunctions
{
    private readonly InMemoryDataStore _store;
    private readonly ILogger<TicketsFunctions> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private const string DefaultAssignee = "support-team@company.com";

    public TicketsFunctions(InMemoryDataStore store, ILogger<TicketsFunctions> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// MCP Tool: list_tickets
    /// Returns IT support tickets filtered by optional status, priority, and assignee.
    /// </summary>
    [Function("ListTickets")]
    public async Task<HttpResponseData> ListTickets(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/tickets")] HttpRequestData req)
    {
        try
        {
            var status = req.Query["status"];
            var assignee = req.Query["assignee"];
            var priority = req.Query["priority"];

            _logger.LogInformation(
                "ListTickets called: status={Status}, assignee={Assignee}, priority={Priority}",
                status, assignee, priority);

            var tickets = _store.GetTicketsByStatus(status, assignee, priority);

            var response = new ApiResponse<List<Ticket>>
            {
                Success = true,
                Data = tickets,
                TotalCount = tickets.Count
            };

            return await OkAsync(req, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ListTickets");
            return await ErrorAsync(req, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// MCP Tool: get_ticket
    /// Returns full details of a specific support ticket by its unique ID.
    /// </summary>
    [Function("GetTicket")]
    public async Task<HttpResponseData> GetTicket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/tickets/{id}")] HttpRequestData req,
        string id)
    {
        try
        {
            _logger.LogInformation("GetTicket called: id={Id}", id);

            var ticket = _store.GetTicket(id);
            if (ticket is null)
            {
                return await NotFoundAsync(req, $"Ticket '{id}' not found.");
            }

            return await OkAsync(req, new ApiResponse<Ticket>
            {
                Success = true,
                Data = ticket,
                TotalCount = 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetTicket for id={Id}", id);
            return await ErrorAsync(req, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// MCP Tool: create_ticket
    /// Creates a new IT support ticket. Always search the knowledge base first
    /// to check whether the issue can be self-resolved before calling this tool.
    /// </summary>
    [Function("CreateTicket")]
    public async Task<HttpResponseData> CreateTicket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/tickets")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("CreateTicket called");

            CreateTicketRequest? createReq;
            try
            {
                createReq = await JsonSerializer.DeserializeAsync<CreateTicketRequest>(
                    req.Body, JsonOptions);
            }
            catch (JsonException)
            {
                return await BadRequestAsync(req, "Request body is not valid JSON.");
            }

            if (createReq is null || string.IsNullOrWhiteSpace(createReq.Title))
            {
                return await BadRequestAsync(req, "title is required.");
            }

            if (string.IsNullOrWhiteSpace(createReq.RequestedBy))
            {
                return await BadRequestAsync(req, "requestedBy is required.");
            }

            var ticket = new Ticket
            {
                Id = $"tkt-{Guid.NewGuid():N}"[..12],
                Title = createReq.Title,
                Description = createReq.Description,
                Priority = createReq.Priority ?? "medium",
                Category = createReq.Category ?? "software",
                Status = "open",
                AssignedTo = DefaultAssignee,
                RequestedBy = createReq.RequestedBy,
                CreatedAt = DateTime.UtcNow
            };

            _store.AddTicket(ticket);

            _logger.LogInformation(
                "Ticket created: id={Id}, priority={Priority}, category={Category}",
                ticket.Id, ticket.Priority, ticket.Category);

            var httpResponse = req.CreateResponse(HttpStatusCode.Created);
            httpResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await httpResponse.WriteStringAsync(JsonSerializer.Serialize(
                new ApiResponse<Ticket>
                {
                    Success = true,
                    Data = ticket,
                    TotalCount = 1,
                    Message = "Ticket created successfully."
                }, JsonOptions));
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateTicket");
            return await ErrorAsync(req, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// MCP Tool: update_ticket_status
    /// Updates the status of an existing support ticket. Setting status to
    /// 'resolved' automatically records the resolution timestamp.
    /// </summary>
    [Function("UpdateTicketStatus")]
    public async Task<HttpResponseData> UpdateTicketStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "api/tickets/{id}/status")] HttpRequestData req,
        string id)
    {
        try
        {
            _logger.LogInformation("UpdateTicketStatus called: id={Id}", id);

            var existing = _store.GetTicket(id);
            if (existing is null)
            {
                return await NotFoundAsync(req, $"Ticket '{id}' not found.");
            }

            UpdateTicketStatusRequest? updateReq;
            try
            {
                updateReq = await JsonSerializer.DeserializeAsync<UpdateTicketStatusRequest>(
                    req.Body, JsonOptions);
            }
            catch (JsonException)
            {
                return await BadRequestAsync(req, "Request body is not valid JSON.");
            }

            if (updateReq is null || string.IsNullOrWhiteSpace(updateReq.Status))
            {
                return await BadRequestAsync(req, "status is required.");
            }

            if (string.IsNullOrWhiteSpace(updateReq.UpdatedBy))
            {
                return await BadRequestAsync(req, "updatedBy is required.");
            }

            // Create updated ticket record
            var updated = new Ticket
            {
                Id = existing.Id,
                Title = existing.Title,
                Description = existing.Description,
                Category = existing.Category,
                Priority = existing.Priority,
                AssignedTo = existing.AssignedTo,
                RequestedBy = existing.RequestedBy,
                CreatedAt = existing.CreatedAt,
                Status = updateReq.Status,
                ResolvedAt = updateReq.Status.Equals("resolved", StringComparison.OrdinalIgnoreCase)
                    ? DateTime.UtcNow
                    : existing.ResolvedAt
            };

            _store.UpdateTicket(updated);

            _logger.LogInformation(
                "Ticket updated: id={Id}, newStatus={Status}, updatedBy={UpdatedBy}",
                id, updateReq.Status, updateReq.UpdatedBy);

            return await OkAsync(req, new ApiResponse<Ticket>
            {
                Success = true,
                Data = updated,
                TotalCount = 1,
                Message = $"Ticket status updated to '{updateReq.Status}'."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateTicketStatus for id={Id}", id);
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
