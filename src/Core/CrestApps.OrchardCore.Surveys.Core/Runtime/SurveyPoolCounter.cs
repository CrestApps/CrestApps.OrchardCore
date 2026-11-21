using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Draws the least-presented questions of an evenly presented pool first. Candidates arrive shuffled, so questions
/// presented as often as each other keep a random order. Counts change under the survey's pool lock.
/// </summary>
public sealed class SurveyPoolCounter : ISurveyPoolCounter
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly SurveyCounterStore _store;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly Dictionary<string, IReadOnlyDictionary<string, int>> _counts = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyPoolCounter"/> class.
    /// </summary>
    /// <param name="store">The counter store.</param>
    /// <param name="distributedLock">The distributed lock.</param>
    /// <param name="clock">The clock.</param>
    public SurveyPoolCounter(
        SurveyCounterStore store,
        IDistributedLock distributedLock,
        IClock clock)
    {
        _store = store;
        _distributedLock = distributedLock;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task PrepareAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return;
        }

        var counter = await _store.FindAsync(surveyId, SurveyCounterKinds.Pools, cancellationToken);
        _counts[surveyId] = counter is null ? new Dictionary<string, int>() : new Dictionary<string, int>(counter.Counts, StringComparer.Ordinal);
        _store.Forget(counter);
    }

    /// <inheritdoc />
    public IReadOnlyList<SurveyQuestion> Order(string surveyId, QuestionPool pool, IReadOnlyList<SurveyQuestion> candidates)
    {
        if (candidates is null || candidates.Count < 2 || surveyId is null || !_counts.TryGetValue(surveyId, out var counts))
        {
            return candidates;
        }

        // OrderBy is stable, so questions with the same count keep the shuffled order they arrived in.
        return candidates
            .OrderBy(question => counts.TryGetValue(question.Id, out var count) ? count : 0)
            .ToList();
    }

    /// <inheritdoc />
    public async Task RecordAsync(string surveyId, IReadOnlyCollection<string> questionIds, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId) || questionIds is null || questionIds.Count == 0)
        {
            return;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync("Surveys:Pools:" + surveyId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return;
        }

        await using (locker)
        {
            var counter = await _store.FindAsync(surveyId, SurveyCounterKinds.Pools, cancellationToken);
            var isNew = counter is null;

            counter ??= new SurveyCounter
            {
                ItemId = IdGenerator.GenerateId(),
                SurveyId = surveyId,
                Kind = SurveyCounterKinds.Pools,
            };

            foreach (var questionId in questionIds)
            {
                counter.Counts[questionId] = (counter.Counts.TryGetValue(questionId, out var count) ? count : 0) + 1;
            }

            counter.ModifiedUtc = _clock.UtcNow;

            if (isNew)
            {
                await _store.CreateAsync(counter, cancellationToken);
            }
            else
            {
                await _store.UpdateAsync(counter, cancellationToken);
            }

            // Saved while the lock is held, so the next respondent reads this draw.
            await _store.CommitAsync(cancellationToken);
        }
    }
}
