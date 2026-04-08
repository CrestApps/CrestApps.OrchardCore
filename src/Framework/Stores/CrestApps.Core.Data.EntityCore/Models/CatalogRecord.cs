namespace CrestApps.Core.Data.EntityCore.Models;

public sealed class CatalogRecord
{
    public string EntityType { get; set; }

    public string ItemId { get; set; }

    public string Name { get; set; }

    public string DisplayText { get; set; }

    public string Source { get; set; }

    public string SessionId { get; set; }

    public string ChatInteractionId { get; set; }

    public string ReferenceId { get; set; }

    public string ReferenceType { get; set; }

    public string AIDocumentId { get; set; }

    public string UserId { get; set; }

    public string Type { get; set; }

    public DateTime? CreatedUtc { get; set; }

    public DateTime? UpdatedUtc { get; set; }

    public string Payload { get; set; }
}
