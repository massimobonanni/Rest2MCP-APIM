namespace ApimMcpDemo.Core.Entities;

/// <summary>Result returned after a restock order is created.</summary>
public class RestockOrderResult
{
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
