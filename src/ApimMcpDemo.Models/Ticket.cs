namespace ApimMcpDemo.Models;

/// <summary>Represents an IT support ticket.</summary>
public class Ticket
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Status values: open, in-progress, resolved, closed</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Priority values: low, medium, high, critical</summary>
    public string Priority { get; set; } = string.Empty;
    /// <summary>Category values: hardware, software, network, access</summary>
    public string Category { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
