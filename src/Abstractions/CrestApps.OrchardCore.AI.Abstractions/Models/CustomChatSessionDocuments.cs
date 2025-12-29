using OrchardCore;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class CustomChatSessionDocuments : Entity
{
    public IList<CustomChatSessionDocumentEntry> Items { get; set; } = [];
}

public sealed class CustomChatSessionDocumentEntry
{
    public string DocumentId { get; set; } = IdGenerator.GenerateId();

    public string FileName { get; set; }

    public string ContentType { get; set; }

    public long Length { get; set; }

    public string TempFilePath { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
