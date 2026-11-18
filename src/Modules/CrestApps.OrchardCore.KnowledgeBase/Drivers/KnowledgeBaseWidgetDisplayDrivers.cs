using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using CrestApps.OrchardCore.KnowledgeBase.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.KnowledgeBase.Drivers;

/// <summary>
/// What the help center's widgets know of the page they are on: the article shown, and the search and category of the
/// help center's page.
/// </summary>
public sealed class KnowledgeBasePageContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IContentManager _contentManager;

    private ContentItem _article;
    private bool _articleRead;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeBasePageContext"/> class.
    /// </summary>
    public KnowledgeBasePageContext(IHttpContextAccessor httpContextAccessor, IContentManager contentManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _contentManager = contentManager;
    }

    /// <summary>
    /// Gets the request.
    /// </summary>
    public HttpContext HttpContext => _httpContextAccessor.HttpContext;

    /// <summary>
    /// Gets the words searched for on the help center's page.
    /// </summary>
    public string Query => HttpContext?.Request.Query["q"].ToString() is { Length: > 0 } query ? query : null;

    /// <summary>
    /// Gets the category of the help center's page.
    /// </summary>
    public string CategoryId => HttpContext?.Request.Query["category"].ToString() is { Length: > 0 } category ? category.Trim() : null;

    /// <summary>
    /// Gets whether the page is the help center's own.
    /// </summary>
    public bool IsHelpCenter => HttpContext?.GetEndpoint()?.Metadata.GetMetadata<RouteNameMetadata>()?.RouteName == KnowledgeBaseConstants.HomeRouteName;

    /// <summary>
    /// Gets the published article the page shows, or <see langword="null"/> on any other page.
    /// </summary>
    /// <returns>The article.</returns>
    public async Task<ContentItem> GetArticleAsync()
    {
        if (_articleRead)
        {
            return _article;
        }

        _articleRead = true;

        if (HttpContext?.GetRouteValue("contentItemId") is string contentItemId
            && HttpContext.GetRouteValue("area") is string area
            && string.Equals(area, "OrchardCore.Contents", StringComparison.Ordinal)
            && await _contentManager.GetAsync(contentItemId, VersionOptions.Published) is { } item
            && item.Has<KnowledgeArticlePart>())
        {
            _article = item;
        }

        return _article;
    }
}

/// <summary>
/// Shows the help center's search box.
/// </summary>
internal sealed class HelpCenterSearchPartDisplayDriver : ContentPartDisplayDriver<HelpCenterSearchPart>
{
    private readonly KnowledgeBasePageContext _page;
    private readonly KnowledgeBaseCategories _categories;

    public HelpCenterSearchPartDisplayDriver(KnowledgeBasePageContext page, KnowledgeBaseCategories categories)
    {
        _page = page;
        _categories = categories;
    }

    public override IDisplayResult Display(HelpCenterSearchPart part, BuildPartDisplayContext context)
        => Initialize<KnowledgeBaseSearchViewModel>("HelpCenterSearchPart", async model =>
        {
            model.Query = _page.Query;

            // On a category's page, the box searches within it.
            if (_page.IsHelpCenter && _page.CategoryId is { } categoryId)
            {
                var tree = await _categories.GetTreeAsync();

                if (tree.Find(categoryId) is not null)
                {
                    model.CategoryId = categoryId;
                    model.CategoryName = tree.GetPathText(categoryId, " › ");
                }
            }
        }).Location("Detail", "Content:5");
}

/// <summary>
/// Shows the help center's categories, with the one of the page or the article picked.
/// </summary>
internal sealed class KnowledgeBaseCategoriesPartDisplayDriver : ContentPartDisplayDriver<KnowledgeBaseCategoriesPart>
{
    private readonly KnowledgeBasePageContext _page;
    private readonly KnowledgeBaseHelpCenter _helpCenter;

    public KnowledgeBaseCategoriesPartDisplayDriver(KnowledgeBasePageContext page, KnowledgeBaseHelpCenter helpCenter)
    {
        _page = page;
        _helpCenter = helpCenter;
    }

    public override IDisplayResult Display(KnowledgeBaseCategoriesPart part, BuildPartDisplayContext context)
        => Initialize<KnowledgeBaseCategoriesViewModel>("KnowledgeBaseCategoriesPart", async model =>
        {
            var user = _page.HttpContext?.User;
            var article = await _page.GetArticleAsync();

            model.Categories = await _helpCenter.GetCategoriesAsync(user, _page.HttpContext?.RequestAborted ?? default);
            model.SelectedId = article is not null ? KnowledgeArticleIndexProvider.CategoryOf(article) : _page.CategoryId;
            model.AllSelected = article is null && _page.IsHelpCenter && _page.CategoryId is null;
        }).Location("Detail", "Content:5");
}

/// <summary>
/// Shows, on an article's page, other articles about the same thing; elsewhere it shows nothing.
/// </summary>
internal sealed class RelatedArticlesPartDisplayDriver : ContentPartDisplayDriver<RelatedArticlesPart>
{
    private readonly KnowledgeBasePageContext _page;
    private readonly KnowledgeBaseHelpCenter _helpCenter;

    internal readonly IStringLocalizer S;

    public RelatedArticlesPartDisplayDriver(KnowledgeBasePageContext page, KnowledgeBaseHelpCenter helpCenter, IStringLocalizer<RelatedArticlesPartDisplayDriver> stringLocalizer)
    {
        _page = page;
        _helpCenter = helpCenter;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> DisplayAsync(RelatedArticlesPart part, BuildPartDisplayContext context)
    {
        if (await _page.GetArticleAsync() is not { } article)
        {
            return null;
        }

        var related = await _helpCenter.GetRelatedAsync(_page.HttpContext?.User, article, part.Count, _page.HttpContext?.RequestAborted ?? default);

        if (related.Count == 0)
        {
            return null;
        }

        return Initialize<RelatedArticlesViewModel>("RelatedArticlesPart", model => model.Articles = related)
            .Location("Detail", "Content:5");
    }

    public override IDisplayResult Edit(RelatedArticlesPart part, BuildPartEditorContext context)
        => Initialize<RelatedArticlesPartViewModel>("RelatedArticlesPart_Edit", model => model.Count = part.Count);

    public override async Task<IDisplayResult> UpdateAsync(RelatedArticlesPart part, UpdatePartEditorContext context)
    {
        var model = new RelatedArticlesPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.Count < 1 || model.Count > RelatedArticlesPart.MaxCount)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Count), S["Show between 1 and {0} articles.", RelatedArticlesPart.MaxCount]);
        }
        else
        {
            part.Count = model.Count;
        }

        return Edit(part, context);
    }
}
