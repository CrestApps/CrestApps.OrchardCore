using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Indexes;

/// <summary>
/// The YesSql index used to query <c>SmsTemplate</c> documents by name.
/// </summary>
public sealed class SmsTemplateIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    public string Name { get; set; }
}
