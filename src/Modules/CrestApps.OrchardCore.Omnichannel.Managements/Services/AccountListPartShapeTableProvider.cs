using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.Lists.ViewModels;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Adds the <c>ListPartNavigationAdmin__Account</c> alternate to the list part's navigation bar when the container
/// is an account. The alternate keeps the list part's buttons and adds <c>List Activities</c>, which lists the
/// activities of every contact in the account.
/// </summary>
internal sealed class AccountListPartShapeTableProvider : IShapeTableProvider
{
    internal const string NavigationAccountAlternate = "ListPartNavigationAdmin__Account";

    /// <inheritdoc/>
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe(ContactListPartShapeTableProvider.NavigationShapeType)
            .OnDisplaying(displaying =>
            {
                if (displaying.Shape is ListPartNavigationAdminViewModel model &&
                    model.Container is not null &&
                    model.ContainerContentTypeDefinition is not null &&
                    model.ContainerContentTypeDefinition.Parts.Any(x => x.PartDefinition?.Name == OmnichannelConstants.ContentParts.Account))
                {
                    displaying.Shape.Metadata.Alternates.Add(NavigationAccountAlternate);
                }
            });

        return ValueTask.CompletedTask;
    }
}
