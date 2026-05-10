namespace NepDateWidget.Models;

public sealed class DocumentEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; }
}
