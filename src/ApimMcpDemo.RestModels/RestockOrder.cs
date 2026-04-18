namespace ApimMcpDemo.RestModels;

/// <summary>Request body for creating a restock order.</summary>
public class RestockOrder
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
