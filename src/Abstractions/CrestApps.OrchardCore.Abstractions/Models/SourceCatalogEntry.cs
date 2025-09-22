namespace CrestApps.OrchardCore.Models;

public class SourceCatalogEntry : CatalogItem, ISourceAwareModel
{
    /// <summary>
    /// Gets the name of the source for this profile.
    /// </summary>
    public string Source { get; set; }
}
