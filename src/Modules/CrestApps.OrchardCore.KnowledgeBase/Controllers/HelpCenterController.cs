using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using CrestApps.OrchardCore.KnowledgeBase.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Locking.Distributed;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.KnowledgeBase.Controllers;

/// <summary>
/// The help center: the categories and the most helpful articles, a category, search, suggestions for other modules, and
/// readers' "was this helpful?" answers.
/// </summary>
public sealed class HelpCenterController : Controller
{
    private const string FeedbackCookiePrefix = "kb-feedback-";

    // The articles the home page shows under each category; the category's page lists them all.
    private const int ArticlesPerGroup = 5;

    private readonly KnowledgeArticleSearch _search;
    private readonly KnowledgeBaseCategories _categories;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDistributedLock _locks;
    private readonly YesSql.ISession _session;

    public HelpCenterController(KnowledgeArticleSearch search, KnowledgeBaseCategories categories, IContentManager contentManager, IAuthorizationService authorizationService, IDistributedLock locks, YesSql.ISession session)
    {
        _locks = locks;
        _session = session;
        _search = search;
        _categories = categories;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// The help center's home, a category or search results.
    /// </summary>
    /// <param name="q">The words searched for.</param>
    /// <param name="category">The category: a term of the categories' taxonomy, whose page lists its articles and those of
    /// the categories under it.</param>
    /// <returns>The page.</returns>
    [HttpGet("help", Name = KnowledgeBaseConstants.HomeRouteName)]
    public async Task<IActionResult> Index(string q = null, string category = null)
    {
        var tree = await _categories.GetTreeAsync();
        var grouped = await _search.ByCategoryAsync(User, ArticlesPerGroup, HttpContext.RequestAborted);
        var selected = string.IsNullOrWhiteSpace(category) ? null : tree.Find(category.Trim());
        var searching = !string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(category);

        // A category counts the articles of the categories under it too, as choosing it lists them.
        int Count(string id)
            => KnowledgeBaseCategories.WithDescendants(tree, id).Sum(value => grouped.TryGetValue(value, out var group) ? group.Count : 0);

        var model = new HelpCenterViewModel
        {
            Query = q,
            CategoryId = selected?.Id ?? category,
            CategoryName = selected is null ? null : tree.GetPathText(selected.Id, " › "),
            Categories = [.. tree.Nodes
                .Select(node => new HelpCenterCategory(node.Id, node.Name, node.Level, Count(node.Id)))
                .Where(value => value.Count > 0)],
            Groups = searching ? [] : [.. tree.Nodes
                .Where(node => grouped.ContainsKey(node.Id))
                .Select(node => new HelpCenterGroup(node.Id, tree.GetPathText(node.Id, " › "), grouped[node.Id].Count, grouped[node.Id].Articles))],
        };

        // A category that isn't one any more finds nothing rather than everything.
        IReadOnlyCollection<string> categoryIds = string.IsNullOrWhiteSpace(category) ? null : selected is null ? ["-"] : KnowledgeBaseCategories.WithDescendants(tree, selected.Id);

        model.Articles = searching || model.Groups.Count == 0
            ? await _search.FindAsync(User, q, categoryIds, 30, HttpContext.RequestAborted)
            : [];

        return View(model);
    }

    /// <summary>
    /// The articles that match what someone is writing, for other modules' forms: title, summary and link.
    /// </summary>
    /// <param name="q">What they wrote so far.</param>
    /// <param name="reader">Who reads the message the link goes in: <c>public</c> (anyone) or <c>signedin</c> (a signed-in
    /// person), so only articles they can open are offered; empty for the user's own.</param>
    /// <returns>Up to five articles.</returns>
    [HttpGet("help/api/suggest", Name = KnowledgeBaseConstants.SuggestRouteName)]
    public async Task<IActionResult> Suggest(string q, string reader = null)
    {
        var readable = reader?.ToLowerInvariant() switch
        {
            "public" => KnowledgeArticleSearch.VisitorReader,
            "signedin" => KnowledgeArticleSearch.SignedInReader,
            _ => null,
        };
        var found = await _search.SuggestAsync(User, q, 5, readable, HttpContext.RequestAborted);

        if (found.Count == 0)
        {
            return Json(Array.Empty<object>());
        }
        var items = await _contentManager.GetAsync(found.Select(article => article.ContentItemId), VersionOptions.Published);
        var paths = items.ToDictionary(item => item.ContentItemId, item => item.TryGet<AutoroutePart>(out var route) ? route.Path : null, StringComparer.Ordinal);

        return Json(found.Select(article => new
        {
            title = article.Title,
            summary = article.Summary,
            url = paths.TryGetValue(article.ContentItemId, out var path) && !string.IsNullOrEmpty(path)
                ? Url.Content("~/" + path.TrimStart('/'))
                : Url.Action("Display", "Item", new { area = "OrchardCore.Contents", contentItemId = article.ContentItemId }),
        }));
    }

    /// <summary>
    /// Records a reader's answer to "was this helpful?", once per article and browser.
    /// </summary>
    /// <param name="id">The article.</param>
    /// <param name="helpful">Their answer.</param>
    /// <param name="returnUrl">Where to go back to.</param>
    /// <returns>A redirect back to the article.</returns>
    [HttpPost("help/articles/{id}/feedback", Name = "KnowledgeBaseFeedback")]
    [RateLimitGroup(KnowledgeBaseConstants.FeedbackRateLimitGroup)]
    public async Task<IActionResult> Feedback(string id, bool helpful, string returnUrl)
    {
        var back = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.RouteUrl(KnowledgeBaseConstants.HomeRouteName);

        if (await _contentManager.GetAsync(id, VersionOptions.Published) is not { } article
            || !article.Has<KnowledgeArticlePart>()
            || !await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, article))
        {
            return NotFound();
        }

        // One answer per browser; a second is thanked but not counted.
        var cookie = FeedbackCookiePrefix + id;

        if (!Request.Cookies.ContainsKey(cookie))
        {
            // Counted under a lock on the article and saved before it is let go, so answers at the same time all count.
            var (locker, locked) = await _locks.TryAcquireLockAsync("KB_FEEDBACK_" + id, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

            if (!locked)
            {
                return Redirect(back + (back.Contains('?') ? "&" : "?") + "feedback=thanks");
            }

            await using var held = locker;

            article = await _contentManager.GetAsync(id, VersionOptions.Published);
            article.Alter<KnowledgeArticlePart>(part =>
            {
                if (helpful)
                {
                    part.HelpfulCount++;
                }
                else
                {
                    part.NotHelpfulCount++;
                }
            });

            await _contentManager.UpdateAsync(article);
            await _session.SaveChangesAsync(HttpContext.RequestAborted);
            Response.Cookies.Append(cookie, "1", new CookieOptions { MaxAge = TimeSpan.FromDays(365), HttpOnly = true, IsEssential = false, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps });
        }

        return Redirect(back + (back.Contains('?') ? "&" : "?") + "feedback=thanks");
    }
}
