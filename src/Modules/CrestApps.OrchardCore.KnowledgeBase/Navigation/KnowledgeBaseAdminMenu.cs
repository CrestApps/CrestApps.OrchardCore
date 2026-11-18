using CrestApps.OrchardCore.KnowledgeBase.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Contents;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.KnowledgeBase.Navigation;

/// <summary>
/// Adds the Knowledge Base menu: the knowledge articles (Orchard Core's content list of the Knowledge Article type, which
/// has its own New button), the categories (the Knowledge Base Categories taxonomy), and the help center at /help. It
/// stands on its own, so a site with a knowledge base and no service desk finds it, and one with both finds it beside
/// the Service Desk menu. Whoever may list content items sees it, as with Orchard Core's own content lists; the list and
/// the editor still ask for the content permissions of the type, and who may read each article is Content Access
/// Control's.
/// </summary>
internal sealed class KnowledgeBaseAdminMenu : AdminNavigationProvider
{
    /// <summary>
    /// The id of the Knowledge Base menu item, which other providers add to.
    /// </summary>
    public const string MenuId = "knowledgeBase";

    private readonly KnowledgeBaseCategories _categories;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeBaseAdminMenu"/> class.
    /// </summary>
    /// <param name="categories">The help center's categories.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public KnowledgeBaseAdminMenu(KnowledgeBaseCategories categories, IStringLocalizer<KnowledgeBaseAdminMenu> stringLocalizer)
    {
        _categories = categories;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var taxonomyId = await _categories.GetTaxonomyIdAsync();

        builder.Add(S["Knowledge Base"], S["Knowledge Base"].PrefixPosition("6.5"), knowledgeBase =>
        {
            knowledgeBase
                .AddClass("knowledge-base")
                .AddClass("icon-class-fa-solid")
                .AddClass("icon-class-fa-book-open")
                .Id(MenuId)
                .Permission(CommonPermissions.ListContent)
                .Add(S["Articles"], S["Articles"].PrefixPosition("1"), articles => articles
                    .Id("knowledgeArticles")
                    .Action("List", "Admin", new RouteValueDictionary
                    {
                        { "area", "OrchardCore.Contents" },
                        { "contentTypeId", KnowledgeBaseConstants.ArticleType },
                    })
                    .Permission(CommonPermissions.ListContent)
                    .LocalNav());

            // The taxonomy the category field uses; there is none until the feature's migration has made it.
            if (taxonomyId is not null)
            {
                knowledgeBase.Add(S["Categories"], S["Categories"].PrefixPosition("2"), categories => categories
                    .Id("knowledgeBaseCategories")
                    .Action("Edit", "Admin", new RouteValueDictionary
                    {
                        { "area", "OrchardCore.Contents" },
                        { "contentItemId", taxonomyId },
                    })
                    .Permission(CommonPermissions.ListContent)
                    .LocalNav());
            }

            knowledgeBase.Add(S["Help center"], S["Help center"].PrefixPosition("9"), helpCenter => helpCenter
                .Id("knowledgeBaseHelpCenter")
                .Url("~/help")
                .Permission(CommonPermissions.ListContent)
                .LocalNav());
        });
    }
}
