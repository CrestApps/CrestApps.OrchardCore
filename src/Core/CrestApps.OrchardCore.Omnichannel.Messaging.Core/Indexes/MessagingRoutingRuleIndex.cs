using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;

/// <summary>
/// Indexes messaging routing rules for the admin list.
/// </summary>
public sealed class MessagingRoutingRuleIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the rule's name.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets whether the rule is used.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the rule's place among the rules.
    /// </summary>
    public int Order { get; set; }
}
