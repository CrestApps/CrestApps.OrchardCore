using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.BusinessDocuments.Navigation;

/// <summary>
/// Adds Settings › Business documents › Document templates and Number series to the admin menu.
/// </summary>
public sealed class BusinessDocumentsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _templatesRouteValues = new()
    {
        { "area", BusinessDocumentsConstants.Features.Area },
        { "controller", "BusinessDocumentTemplates" },
        { "action", "Index" },
    };

    private static readonly RouteValueDictionary _numberSeriesRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", BusinessDocumentsConstants.NumberSeriesSettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BusinessDocumentsAdminMenu(IStringLocalizer<BusinessDocumentsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Business documents"], S["Business documents"].PrefixPosition(), documents => documents
                    .Add(S["Document templates"], S["Document templates"].PrefixPosition(), templates => templates
                        .Id("businessDocumentTemplates")
                        .Action(_templatesRouteValues)
                        .Permission(BusinessDocumentsPermissions.ManageBusinessDocumentTemplates)
                        .LocalNav()
                    )
                    .Add(S["Number series"], S["Number series"].PrefixPosition(), series => series
                        .Id("numberSeries")
                        .Action("Index", "Admin", _numberSeriesRouteValues)
                        .Permission(BusinessDocumentsPermissions.ManageNumberSeries)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
