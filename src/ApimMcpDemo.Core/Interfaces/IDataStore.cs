using ApimMcpDemo.Core.Entities;

namespace ApimMcpDemo.Core.Interfaces;

/// <summary>
/// Abstraction over the data store used by all function handlers.
/// </summary>
public interface IDataStore
{
    // Products
    List<Product> SearchProducts(string? query, string? category);
    Product? GetProduct(string id);

    // Inventory / stock
    StockLevel? GetStockLevel(string productId);
    void AddRestockOrder(RestockOrderResult order);
    List<RestockOrderResult> GetRestockOrders();

    // Tickets
    List<Ticket> GetTicketsByStatus(string? status, string? assignee, string? priority = null);
    Ticket? GetTicket(string id);
    void AddTicket(Ticket ticket);
    bool UpdateTicket(Ticket updated);

    // Knowledge base
    List<KbArticle> SearchKbArticles(string query, string? category = null);
    KbArticle? GetKbArticle(string id);
}
