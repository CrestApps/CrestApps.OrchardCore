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
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDistributedLock _locks;
    private readonly YesSql.ISession _session;

    public HelpCenterController(KnowledgeArticleSearch search, IContentManager contentManager, IAuthorizationService authorizationService, IDistributedLock locks, YesSql.ISession session)
    {
        _locks = locks;
        _session = session;
        _search = search;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// The help center's home, a category or search results.
    /// </summary>
    /// <param name="q">The words searched for.</param>
    /// <param name="category">The category.</param>
    /// <returns>The page.</returns>
    [HttpGet("help", Name = KnowledgeBaseConstants.HomeRouteName)]
    public async Task<IActionResult> Index(string q = null, string category = null)
        => View(new HelpCenterViewModel
        {
            Query = q,
            Category = category,
            Categories = await _search.CategoriesAsync(User, HttpContext.RequestAborted),
            Articles = await _search.FindAsync(User, q, category, 30, HttpContext.RequestAborted),
        });

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
        IReadOnlyCollection<KnowledgeArticleAudience> readable = reader?.ToLowerInvariant() switch
        {
            "public" => [KnowledgeArticleAudience.Public],
            "signedin" => [KnowledgeArticleAudience.Public, KnowledgeArticleAudience.SignedIn],
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
