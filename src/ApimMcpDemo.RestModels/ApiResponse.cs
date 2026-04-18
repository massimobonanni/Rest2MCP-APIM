namespace ApimMcpDemo.RestModels;

/// <summary>Generic API response wrapper.</summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public int TotalCount { get; set; }
}
