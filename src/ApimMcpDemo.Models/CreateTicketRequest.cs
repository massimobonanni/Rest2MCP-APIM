namespace ApimMcpDemo.Models;

/// <summary>Request body for creating a new IT support ticket.</summary>
public class CreateTicketRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
}
