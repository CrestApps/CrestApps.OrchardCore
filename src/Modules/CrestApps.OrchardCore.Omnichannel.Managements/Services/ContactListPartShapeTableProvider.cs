using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.Lists.ViewModels;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Adapts the <c>ListPart</c> admin shapes to a container that is a contact.
/// <list type="bullet">
/// <item>
/// <description>
/// Adds the <c>ListPartNavigationAdmin__OmnichannelContact</c> alternate to the list part's navigation bar. The list
/// part renders its own bar and offers no way to extend it, so without this alternate a contact that is also a list
/// shows two bars: the list part's bar and the contact's activity bar. The alternate renders a single bar that
/// carries both sets of buttons.
/// </description>
/// </item>
/// <item>
/// <description>
/// Adds the <c>Content_HeaderAdmin__OmnichannelContact</c> alternate to the contact's header, which the list part
/// shows above the list when its header is enabled. Orchard Core's header view renders only the display text; the
/// alternate also renders the header's content zone, where the contact's primary contact methods are placed.
/// </description>
/// </item>
/// </list>
/// </summary>
internal sealed class ContactListPartShapeTableProvider : IShapeTableProvider
{
    /// <summary>
    /// The technical name of the Orchard Core list part.
    /// </summary>
    internal const string ListPartName = "ListPart";

    internal const string NavigationShapeType = "ListPartNavigationAdmin";

    internal const string NavigationContactAlternate = "ListPartNavigationAdmin__OmnichannelContact";

    internal const string HeaderShapeType = "Content_HeaderAdmin";

    internal const string HeaderContactAlternate = "Content_HeaderAdmin__OmnichannelContact";

    /// <inheritdoc/>
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe(NavigationShapeType)
            .OnDisplaying(displaying =>
            {
                if (displaying.Shape is ListPartNavigationAdminViewModel model &&
                    model.Container is not null &&
                    model.ContainerContentTypeDefinition is not null &&
                    model.ContainerContentTypeDefinition.Parts.Any(x => x.Name == OmnichannelConstants.ContentParts.OmnichannelContact))
                {
                    displaying.Shape.Metadata.Alternates.Add(NavigationContactAlternate);
                }
            });

        builder.Describe(HeaderShapeType)
            .OnDisplaying(displaying =>
            {
                if (!displaying.Shape.TryGetProperty<ContentItem>("ContentItem", out var contentItem) ||
                    contentItem is null ||
                    !contentItem.Has<OmnichannelContactPart>())
                {
                    return;
                }

                // Alternates later in the collection win. The contact alternate goes first so a template a site
                // made for its own contact type, such as Content-Customer.HeaderAdmin, still takes precedence.
                var alternates = displaying.Shape.Metadata.Alternates;
                var existing = alternates.ToArray();

                alternates.Clear();
                alternates.Add(HeaderContactAlternate);
                alternates.AddRange(existing);
            });

        return ValueTask.CompletedTask;
    }
}
