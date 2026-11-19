using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;

/// <summary>
/// The YesSql index used to query <c>MessageTemplate</c> documents by name.
/// </summary>
public sealed class MessageTemplateIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the kind of work the template is for; empty for the messaging workspace.
    /// </summary>
    public string Scope { get; set; }
}
