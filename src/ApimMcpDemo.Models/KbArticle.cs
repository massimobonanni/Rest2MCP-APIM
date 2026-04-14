namespace ApimMcpDemo.Models;

/// <summary>Represents a knowledge base article.</summary>
public class KbArticle
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string Category { get; set; } = string.Empty;
    public int HelpfulVotes { get; set; }
}
