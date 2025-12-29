namespace CrestApps.OrchardCore.AI.Tools.Models;

public sealed class DocumentReaderToolMetadata
{
    public string DocumentId { get; set; }

    public int MaxWords { get; set; } = 200;
}
