using System.Globalization;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.PersonalData;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.KnowledgeBase.Services;

/// <summary>
/// A signed-in person's answers to "Was this helpful?" in a personal data request: an export lists each answer and what
/// they said was missing; an erasure removes who answered and rewrites the comment so nothing names them, and keeps the
/// answer itself for the article's counts.
/// </summary>
internal sealed class KnowledgeArticleFeedbackPersonalDataContributor : IPersonalDataContributor
{
    public const string FeedbackKind = "KnowledgeArticleFeedback";

    private readonly ISession _session;

    public KnowledgeArticleFeedbackPersonalDataContributor(ISession session)
    {
        _session = session;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var feedback in await ListAsync(context.Subject, cancellationToken))
        {
            var record = context.Add(FeedbackKind, feedback.ItemId, feedback.Title, feedback.CreatedUtc);
            record.Fields[nameof(KnowledgeArticleFeedback.ContentItemId)] = feedback.ContentItemId;
            record.Fields[nameof(KnowledgeArticleFeedback.Helpful)] = feedback.Helpful.ToString(CultureInfo.InvariantCulture);
            record.Fields[nameof(KnowledgeArticleFeedback.Comment)] = feedback.Comment;
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var feedback in await ListAsync(context.Subject, cancellationToken))
        {
            feedback.UserId = null;
            feedback.UserName = null;
            feedback.Comment = context.Scrub(feedback.Comment);

            await _session.SaveAsync(feedback, collection: KnowledgeBaseConstants.CollectionName, cancellationToken: cancellationToken);
            context.Result.Count(FeedbackKind);
        }
    }

    private async Task<IEnumerable<KnowledgeArticleFeedback>> ListAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var userIds = subject.UserIds.Where(userId => !string.IsNullOrEmpty(userId)).ToArray();

        if (userIds.Length == 0)
        {
            return [];
        }

        return await _session.Query<KnowledgeArticleFeedback, KnowledgeArticleFeedbackIndex>(index => index.UserId.IsIn(userIds), KnowledgeBaseConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .ListAsync(cancellationToken);
    }
}
