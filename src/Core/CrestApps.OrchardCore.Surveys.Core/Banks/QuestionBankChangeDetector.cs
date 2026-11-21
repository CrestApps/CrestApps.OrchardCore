using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Banks;

/// <summary>
/// Compares the bank questions a published version copied with their current revisions.
/// </summary>
public sealed class QuestionBankChangeDetector : IQuestionBankChangeDetector
{
    private readonly ISurveyVersionCache _versions;
    private readonly BankQuestionStore _questions;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionBankChangeDetector"/> class.
    /// </summary>
    /// <param name="versions">The version cache.</param>
    /// <param name="questions">The bank question store.</param>
    /// <param name="session">The YesSql session.</param>
    public QuestionBankChangeDetector(
        ISurveyVersionCache versions,
        BankQuestionStore questions,
        ISession session)
    {
        _versions = versions;
        _questions = questions;
        _session = session;
    }

    /// <inheritdoc />
    public async Task<SurveyBankChange> GetChangeAsync(Survey survey, CancellationToken cancellationToken = default)
    {
        var version = await _versions.GetPublishedAsync(survey, cancellationToken);

        if (version?.Definition is null)
        {
            return null;
        }

        var copied = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var question in version.Definition.Pages.SelectMany(page => page.Questions))
        {
            if (!string.IsNullOrEmpty(question?.Id) && BankQuestionRevisions.TryGet(question, out var revision))
            {
                copied[question.Id] = revision;
            }
        }

        if (copied.Count == 0)
        {
            return null;
        }

        var current = (await _questions.ListByIdsAsync(copied.Keys.ToArray(), cancellationToken))
            .ToDictionary(question => question.ItemId, StringComparer.Ordinal);

        var change = new SurveyBankChange
        {
            SurveyId = survey.ItemId,
            VersionNumber = version.Number,
        };

        foreach (var (id, revision) in copied)
        {
            if (!current.TryGetValue(id, out var question) || question.Retired)
            {
                change.RemovedQuestionIds.Add(id);
            }
            else if (question.Revision != revision)
            {
                change.ChangedQuestionIds.Add(id);
            }
        }

        return change.ChangedQuestionIds.Count == 0 && change.RemovedQuestionIds.Count == 0 ? null : change;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SurveyBankChange>> GetChangedSurveysAsync(CancellationToken cancellationToken = default)
    {
        var changes = new List<SurveyBankChange>();
        var skip = 0;

        while (true)
        {
            var page = await _session.Query<Survey, SurveyIndex>(index => index.PublishedVersionNumber > 0, collection: SurveysConstants.CollectionName)
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(SurveysConstants.PageSize)
                .ListAsync(cancellationToken);

            var surveys = page.ToList();

            foreach (var survey in surveys)
            {
                if (await GetChangeAsync(survey, cancellationToken) is { } change)
                {
                    changes.Add(change);
                }
            }

            if (surveys.Count < SurveysConstants.PageSize)
            {
                break;
            }

            skip += surveys.Count;
        }

        return changes;
    }
}
