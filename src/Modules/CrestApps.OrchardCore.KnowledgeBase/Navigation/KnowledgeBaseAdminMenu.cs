using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Contents;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.KnowledgeBase.Navigation;

/// <summary>
/// Adds Knowledge Base to the Service Desk menu: the knowledge articles (Orchard Core's content list of the Knowledge
/// Article type, which has its own New button) and the help center at /help. Whoever may list content items sees it,
/// as with Orchard Core's own content lists; the list and the editor still ask for the content permissions of the type,
/// and who may read each article is Content Access Control's.
/// </summary>
internal sealed class KnowledgeBaseAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeBaseAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public KnowledgeBaseAdminMenu(IStringLocalizer<KnowledgeBaseAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        // The same Service Desk menu the Tickets feature adds (same name, position and id), so the two merge.
        builder.Add(S["Service Desk"], S["Service Desk"].PrefixPosition("6"), serviceDesk => serviceDesk
            .AddClass("service-desk")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-headset")
            .Id("serviceDesk")
            .Add(S["Knowledge Base"], S["Knowledge Base"].PrefixPosition("1.8"), knowledgeBase => knowledgeBase
                .Id("knowledgeBase")
                .Permission(CommonPermissions.ListContent)
                .Add(S["Articles"], S["Articles"].PrefixPosition("1"), articles => articles
                    .Id("knowledgeArticles")
                    .Action("List", "Admin", new RouteValueDictionary
                    {
                        { "area", "OrchardCore.Contents" },
                        { "contentTypeId", KnowledgeBaseConstants.ArticleType },
                    })
                    .Permission(CommonPermissions.ListContent)
                    .LocalNav())
                .Add(S["Help center"], S["Help center"].PrefixPosition("2"), helpCenter => helpCenter
                    .Id("knowledgeBaseHelpCenter")
                    .Url("~/help")
                    .Permission(CommonPermissions.ListContent)
                    .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
