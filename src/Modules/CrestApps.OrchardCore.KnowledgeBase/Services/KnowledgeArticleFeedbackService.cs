using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.KnowledgeBase.Services;

/// <summary>
/// Keeps readers' answers to "Was this helpful?": a dated record of each answer in the Knowledge Base's collection, and
/// the counters on the article the help center sorts by.
/// </summary>
public sealed class KnowledgeArticleFeedbackService
{
    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeArticleFeedbackService"/> class.
    /// </summary>
    public KnowledgeArticleFeedbackService(ISession session, IContentManager contentManager, IClock clock)
    {
        _session = session;
        _contentManager = contentManager;
        _clock = clock;
    }

    /// <summary>
    /// Records an answer on the published article: the record, and one more on its helpful or not helpful counter. The
    /// caller holds the article's lock and saves the session.
    /// </summary>
    /// <param name="article">The published article.</param>
    /// <param name="helpful">Whether it helped.</param>
    /// <param name="user">The reader.</param>
    /// <returns>The answer's id, which lets the same reader say what was missing.</returns>
    public async Task<string> RecordAsync(ContentItem article, bool helpful, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(article);

        var signedIn = user?.Identity?.IsAuthenticated == true;
        var feedback = new KnowledgeArticleFeedback
        {
            ItemId = UniqueId.GenerateId(),
            ContentItemId = article.ContentItemId,
            ContentItemVersionId = article.ContentItemVersionId,
            Title = article.DisplayText,
            CategoryId = KnowledgeArticleIndexProvider.CategoryOf(article),
            Helpful = helpful,
            CreatedUtc = _clock.UtcNow,
            UserId = signedIn ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null,
            UserName = signedIn ? user.Identity.Name : null,
        };

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
        await _session.SaveAsync(feedback, collection: KnowledgeBaseConstants.CollectionName);

        return feedback.ItemId;
    }

    /// <summary>
    /// Adds what was missing to a reader's "No", once: only to that answer on that article, and only while it has no
    /// comment yet.
    /// </summary>
    /// <param name="contentItemId">The article.</param>
    /// <param name="feedbackId">The answer's id, as given to the reader.</param>
    /// <param name="comment">What was missing.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether the comment was kept.</returns>
    public async Task<bool> AddCommentAsync(string contentItemId, string feedbackId, string comment, CancellationToken cancellationToken = default)
    {
        var text = Clean(comment);

        if (text is null || string.IsNullOrEmpty(contentItemId) || string.IsNullOrEmpty(feedbackId))
        {
            return false;
        }

        var feedback = await _session.Query<KnowledgeArticleFeedback, KnowledgeArticleFeedbackIndex>(
            index => index.ItemId == feedbackId && index.ContentItemId == contentItemId,
            KnowledgeBaseConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

        if (feedback is null || feedback.Helpful || !string.IsNullOrEmpty(feedback.Comment))
        {
            return false;
        }

        feedback.Comment = text;
        await _session.SaveAsync(feedback, collection: KnowledgeBaseConstants.CollectionName, cancellationToken: cancellationToken);

        return true;
    }

    /// <summary>
    /// Trims a comment and cuts it to <see cref="KnowledgeArticleFeedbackIndex.MaxCommentLength"/>.
    /// </summary>
    /// <param name="comment">The comment.</param>
    /// <returns>The comment to keep, or <see langword="null"/> when it is empty.</returns>
    internal static string Clean(string comment)
    {
        var text = comment?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length > KnowledgeArticleFeedbackIndex.MaxCommentLength ? text[..KnowledgeArticleFeedbackIndex.MaxCommentLength] : text;
    }
}
