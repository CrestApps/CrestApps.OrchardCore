using CrestApps.OrchardCore.Subscriptions.Core;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Defines the subscription summary dashboard widget content type.
/// </summary>
public sealed class SubscriptionSummaryWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionSummaryWidgetMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to update dashboard widget definitions.</param>
    public SubscriptionSummaryWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates the subscription summary part and dashboard widget content type definitions.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("SubscriptionSummaryPart", part => part
            .WithDisplayName("Subscription Summary")
            .WithDescription("Displays a live summary of subscription statistics.")
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync(SubscriptionConstants.SubscriptionSummaryWidgetType, type => type
            .Stereotype("DashboardWidget")
            .WithDisplayName("Subscription Summary")
            .WithDescription("Shows subscription totals and revenue on the admin dashboard.")
            .Draftable(false)
            .Versionable(false)
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("SubscriptionSummaryPart", part => part.WithPosition("1"))
        );

        return 1;
    }
}
