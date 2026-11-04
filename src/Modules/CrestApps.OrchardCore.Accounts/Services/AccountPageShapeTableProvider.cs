using CrestApps.OrchardCore.Accounts.Models;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Gives an account's page its own layout: the <c>ContentZone__AccountPage</c> alternate of the page's Content zone lists
/// the page's tabs down the side instead of across the top, so the tabs that modules add never wrap. The page keeps
/// Orchard Core's own template, with its title, breadcrumb and header.
/// </summary>
internal sealed class AccountPageShapeTableProvider : IShapeTableProvider
{
    internal const string Alternate = "ContentZone__AccountPage";

    /// <inheritdoc/>
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        // The admin's content page builds the item as Content_DetailAdmin; the plain Content shape is described too in
        // case a display type is rendered through it.
        foreach (var shapeType in new[] { "Content", "Content_DetailAdmin" })
        {
            builder.Describe(shapeType)
                .OnDisplaying(displaying =>
                {
                    if (displaying.Shape.Metadata.DisplayType == OrchardCoreConstants.DisplayType.DetailAdmin &&
                        displaying.Shape.Properties.TryGetValue("ContentItem", out var value) &&
                        value is ContentItem contentItem &&
                        contentItem.Has<AccountPart>() &&
                        displaying.Shape.Properties.TryGetValue("Content", out var zone) &&
                        zone is IShape contentZone &&
                        !contentZone.Metadata.Alternates.Contains(Alternate))
                    {
                        contentZone.Metadata.Alternates.Add(Alternate);
                    }
                });
        }

        return ValueTask.CompletedTask;
    }
}
