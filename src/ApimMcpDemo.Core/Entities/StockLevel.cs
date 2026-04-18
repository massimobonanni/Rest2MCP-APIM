namespace ApimMcpDemo.Core.Entities;

/// <summary>Represents current stock level for a product.</summary>
public class StockLevel
{
    public string ProductId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int QuantityAvailable { get; set; }
    public int ReorderThreshold { get; set; }
    public string WarehouseLocation { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}
