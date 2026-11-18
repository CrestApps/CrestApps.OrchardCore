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

    private readonly KnowledgeArticleSearch _search;
    private readonly KnowledgeBaseCategories _categories;
    private readonly KnowledgeBaseHelpCenter _helpCenter;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDistributedLock _locks;
    private readonly YesSql.ISession _session;
    private readonly KnowledgeArticleFeedbackService _feedback;

    public HelpCenterController(KnowledgeArticleSearch search, KnowledgeBaseCategories categories, IContentManager contentManager, IAuthorizationService authorizationService, IDistributedLock locks, YesSql.ISession session, KnowledgeArticleFeedbackService feedback, KnowledgeBaseHelpCenter helpCenter)
    {
        _feedback = feedback;
        _helpCenter = helpCenter;
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
        var grouped = await _helpCenter.GetGroupedAsync(User, HttpContext.RequestAborted);
        var selected = string.IsNullOrWhiteSpace(category) ? null : tree.Find(category.Trim());
        var searching = !string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(category);

        var model = new HelpCenterViewModel
        {
            Query = q,
            CategoryId = selected?.Id ?? category,
            CategoryName = selected is null ? null : tree.GetPathText(selected.Id, " › "),
            Categories = await _helpCenter.GetCategoriesAsync(User, HttpContext.RequestAborted),
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

        if (Request.Cookies.ContainsKey(cookie))
        {
            return Redirect(WithFeedback(back, "thanks"));
        }

        // Counted under a lock on the article and saved before it is let go, so answers at the same time all count.
        var (locker, locked) = await _locks.TryAcquireLockAsync("KB_FEEDBACK_" + id, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

        if (!locked)
        {
            return Redirect(WithFeedback(back, "thanks"));
        }

        await using (locker)
        {
            article = await _contentManager.GetAsync(id, VersionOptions.Published);
            var feedbackId = await _feedback.RecordAsync(article, helpful, User);
            await _session.SaveChangesAsync(HttpContext.RequestAborted);

            // The answer's id lets this browser, and only it, say what was missing after a No.
            Response.Cookies.Append(cookie, feedbackId, new CookieOptions { MaxAge = TimeSpan.FromDays(365), HttpOnly = true, IsEssential = false, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps });
        }

        return Redirect(WithFeedback(back, helpful ? "thanks" : "missing"));
    }

    /// <summary>
    /// Keeps what a reader said was missing after answering No: once, on their own answer.
    /// </summary>
    /// <param name="id">The article.</param>
    /// <param name="comment">What was missing.</param>
    /// <param name="returnUrl">Where to go back to.</param>
    /// <returns>A redirect back to the article.</returns>
    [HttpPost("help/articles/{id}/feedback/comment", Name = "KnowledgeBaseFeedbackComment")]
    [RateLimitGroup(KnowledgeBaseConstants.FeedbackRateLimitGroup)]
    public async Task<IActionResult> FeedbackComment(string id, string comment, string returnUrl)
    {
        var back = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.RouteUrl(KnowledgeBaseConstants.HomeRouteName);

        if (Request.Cookies.TryGetValue(FeedbackCookiePrefix + id, out var feedbackId)
            && await _feedback.AddCommentAsync(id, feedbackId, comment, HttpContext.RequestAborted))
        {
            await _session.SaveChangesAsync(HttpContext.RequestAborted);
        }

        return Redirect(WithFeedback(back, "thanks"));
    }

    private static string WithFeedback(string url, string value)
    {
        var anchor = url.IndexOf('#', StringComparison.Ordinal);
        var path = anchor < 0 ? url : url[..anchor];

        return path + (path.Contains('?') ? "&" : "?") + "feedback=" + value + "#kb-feedback";
    }
}
