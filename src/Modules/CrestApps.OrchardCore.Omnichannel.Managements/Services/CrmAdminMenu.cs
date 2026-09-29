using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Adds the Leads, Accounts and Opportunities lists to the Interaction Center menu, and the lead status and
/// opportunity stage catalogs to its Management menu. Each list is Orchard Core's content list scoped to the types
/// of that kind, the same way the Contacts list is, and appears only when at least one such type exists.
/// </summary>
internal sealed class CrmAdminMenu : AdminNavigationProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrmAdminMenu"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to warm the content type cache.</param>
    /// <param name="contentTypeProvider">The provider that exposes the cached CRM content types.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CrmAdminMenu(
        IContentDefinitionManager contentDefinitionManager,
        OmnichannelContentTypeProvider contentTypeProvider,
        IStringLocalizer<CrmAdminMenu> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentTypeProvider = contentTypeProvider;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        await _contentTypeProvider.EnsureInitializedAsync(_contentDefinitionManager);

        var leadTypes = _contentTypeProvider.GetLeadContentTypes();
        var accountTypes = _contentTypeProvider.GetAccountContentTypes();
        var opportunityTypes = _contentTypeProvider.GetOpportunityContentTypes();

        builder
            .Add(S["Interaction Center"], "80", interactionCenter =>
            {
                interactionCenter
                    .AddClass("interaction-center")
                    .Id("interactionCenter");

                if (leadTypes.Count > 0)
                {
                    // Converted leads leave the list the moment they are converted, as they do in Salesforce; the
                    // filter can be cleared to find them.
                    interactionCenter.Add(S["Leads"], S["Leads"].PrefixPosition(), leads => leads
                        .AddClass("leads")
                        .Id("leads")
                        .Action("List", "Admin", ContentList(leadTypes, "converted:false"))
                        .Permission(CommonPermissions.ListContent)
                        .LocalNav());
                }

                if (accountTypes.Count > 0)
                {
                    interactionCenter.Add(S["Accounts"], S["Accounts"].PrefixPosition(), accounts => accounts
                        .AddClass("accounts")
                        .Id("accounts")
                        .Action("List", "Admin", ContentList(accountTypes))
                        .Permission(CommonPermissions.ListContent)
                        .LocalNav());
                }

                if (opportunityTypes.Count > 0)
                {
                    interactionCenter.Add(S["Opportunities"], S["Opportunities"].PrefixPosition(), opportunities => opportunities
                        .AddClass("opportunities")
                        .Id("opportunities")
                        .Action("List", "Admin", ContentList(opportunityTypes, "closed:false"))
                        .Permission(CommonPermissions.ListContent)
                        .LocalNav());
                }

                interactionCenter.Add(S["Management"], S["Management"].PrefixPosition(), management => management
                    .AddClass("interaction-center-management")
                    .Id("interactionCenterManagement")
                    .Add(S["Lead Statuses"], S["Lead Statuses"].PrefixPosition(), statuses => statuses
                        .AddClass("lead-statuses")
                        .Id("leadStatuses")
                        .Action("Index", "LeadStatuses", OmnichannelConstants.Features.Managements)
                        .Permission(OmnichannelConstants.Permissions.ManageLeadStatuses)
                        .LocalNav())
                    .Add(S["Opportunity Stages"], S["Opportunity Stages"].PrefixPosition(), stages => stages
                        .AddClass("opportunity-stages")
                        .Id("opportunityStages")
                        .Action("Index", "OpportunityStages", OmnichannelConstants.Features.Managements)
                        .Permission(OmnichannelConstants.Permissions.ManageOpportunityStages)
                        .LocalNav()));
            }, priority: 1);
    }

    private static RouteValueDictionary ContentList(IReadOnlyCollection<string> contentTypes, string filter = null)
    {
        var routeValues = new RouteValueDictionary
        {
            { "area", "OrchardCore.Contents" },
            { "contentTypeId", string.Join(',', contentTypes) },
        };

        if (!string.IsNullOrEmpty(filter))
        {
            routeValues["q"] = filter;
        }

        return routeValues;
    }
}
