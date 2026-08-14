using CrestApps.OrchardCore.Subscriptions.Core;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

public sealed class SubscriptionSummaryWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SubscriptionSummaryWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("SubscriptionSummaryPart", part => part
            .WithDisplayName("Subscription Summary")
            .WithDescription("Displays a live summary of subscription statistics.")
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync(SubscriptionConstants.SubscriptionSummaryWidgetType, type => type
            .Stereotype("DashboardWidget")
            .DisplayedAs("Subscription Summary")
            .WithDescription("Shows subscription totals and revenue on the admin dashboard.")
            .Draftable(false)
            .Versionable(false)
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("SubscriptionSummaryPart", part => part.WithPosition("1"))
        );

        return 1;
    }
}
