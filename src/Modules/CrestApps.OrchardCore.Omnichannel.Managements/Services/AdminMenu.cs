using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal sealed class AdminMenu : AdminNavigationProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminMenu"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to warm the contact content type cache.</param>
    /// <param name="contentTypeProvider">The provider that exposes the cached omnichannel contact content types.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdminMenu(
        IContentDefinitionManager contentDefinitionManager,
        OmnichannelContentTypeProvider contentTypeProvider,
        IStringLocalizer<AdminMenu> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentTypeProvider = contentTypeProvider;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        await _contentTypeProvider.EnsureInitializedAsync(_contentDefinitionManager);

        var contactContentTypes = _contentTypeProvider.GetContactContentTypes();

        builder
            .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                .AddClass("interaction-center")
                .Id("interactionCenter")
                .Add(S["Activities"], S["Activities"].PrefixPosition(), activities => activities
                    .AddClass("activities")
                    .Id("activities")
                    .Action("Activities", "Activities", "CrestApps.OrchardCore.Omnichannel.Managements")
                    .Permission(OmnichannelConstants.Permissions.ListActivities)
                    .LocalNav()
                )
                .Add(S["Contacts"], S["Contacts"].PrefixPosition(), contacts =>
                {
                    contacts
                        .AddClass("contacts")
                        .Id("contacts");

                    if (contactContentTypes.Count > 0)
                    {
                        contacts
                            .Action("List", "Admin", new RouteValueDictionary
                            {
                                { "area", "OrchardCore.Contents" },
                                { "contentTypeId", string.Join(',', contactContentTypes) },
                            });
                    }

                    contacts
                        .Permission(CommonPermissions.ListContent)
                        .LocalNav();
                })
                .Add(S["Management"], S["Management"].PrefixPosition(), management => management
                    .AddClass("interaction-center-management")
                    .Id("interactionCenterManagement")
                    .Add(S["Manage Activities"], S["Manage Activities"].PrefixPosition(), manageActivities => manageActivities
                        .AddClass("manage-activities")
                        .Id("manageActivities")
                        .Action("ManageActivities", "Activities", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageActivities)
                        .LocalNav())
                    .Add(S["Load Inventory"], S["Load Inventory"].PrefixPosition(), inventory => inventory
                        .AddClass("activity-batches")
                        .Id("activityBatches")
                        .Action("Index", "ActivityBatches", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageActivityBatches)
                        .LocalNav())
                    .Add(S["Subject Flows"], S["Subject Flows"].PrefixPosition(), subjectFlows => subjectFlows
                        .AddClass("subject-flows")
                        .Id("subjectFlows")
                        .Action("Index", "SubjectFlows", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageSubjectFlows)
                        .LocalNav())
                    .Add(S["Campaigns"], S["Campaigns"].PrefixPosition(), campaigns => campaigns
                        .AddClass("campaigns")
                        .Id("campaigns")
                        .Action("Index", "Campaigns", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageCampaigns)
                        .LocalNav())
                    .Add(S["Campaign Groups"], S["Campaign Groups"].PrefixPosition(), campaignGroups => campaignGroups
                        .AddClass("campaign-groups")
                        .Id("campaignGroups")
                        .Action("Index", "CampaignGroups", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageCampaignGroups)
                        .LocalNav())
                    .Add(S["Dispositions"], S["Dispositions"].PrefixPosition(), dispositions => dispositions
                        .AddClass("dispositions")
                        .Id("dispositions")
                        .Action("Index", "Dispositions", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageDispositions)
                        .LocalNav())
                ),
                priority: 1);
    }
}
