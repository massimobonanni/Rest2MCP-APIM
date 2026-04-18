namespace ApimMcpDemo.RestModels;

/// <summary>Request body for updating the status of a support ticket.</summary>
public class UpdateTicketStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}
