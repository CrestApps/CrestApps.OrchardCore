using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Options;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Stores AI test cases.
/// </summary>
public interface IAITestCaseStore : ICatalog<AITestCase>
{
}

/// <summary>
/// Stores AI test runs.
/// </summary>
public interface IAITestRunStore : ICatalog<AITestRun>
{
    /// <summary>
    /// Lists a page of a test's runs, newest first.
    /// </summary>
    /// <param name="testCaseId">The test, or <see langword="null"/> for every test.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The runs and how many there are in all.</returns>
    Task<(IReadOnlyList<AITestRun> Runs, int Count)> PageAsync(string testCaseId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the runs waiting for the background runner, oldest first.
    /// </summary>
    /// <param name="take">How many to list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The runs.</returns>
    Task<IReadOnlyList<AITestRun>> ListQueuedAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the runs of a batch.
    /// </summary>
    /// <param name="batchId">The batch.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The runs.</returns>
    Task<IReadOnlyList<AITestRun>> ListByBatchAsync(string batchId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores AI test cases in the AI collection.
/// </summary>
public sealed class AITestCaseStore : DocumentCatalog<AITestCase, AITestCaseIndex>, IAITestCaseStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AITestCaseStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AITestCaseStore(ISession session, IOptions<YesSqlStoreOptions> options)
        : base(session)
    {
        CollectionName = options.Value.AICollectionName;
    }
}

/// <summary>
/// Stores AI test runs in the AI collection.
/// </summary>
public sealed class AITestRunStore : DocumentCatalog<AITestRun, AITestRunIndex>, IAITestRunStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AITestRunStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AITestRunStore(ISession session, IOptions<YesSqlStoreOptions> options)
        : base(session)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<AITestRun> Runs, int Count)> PageAsync(string testCaseId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrEmpty(testCaseId)
            ? Session.Query<AITestRun, AITestRunIndex>(collection: CollectionName)
            : Session.Query<AITestRun, AITestRunIndex>(index => index.TestCaseId == testCaseId, collection: CollectionName);

        var count = await query.CountAsync(cancellationToken);
        var runs = await query
            .OrderByDescending(index => index.QueuedUtc)
            .Skip(Math.Max(0, page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync(cancellationToken);

        return (runs.ToList(), count);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AITestRun>> ListQueuedAsync(int take, CancellationToken cancellationToken = default)
    {
        var runs = await Session.Query<AITestRun, AITestRunIndex>(index => index.Status == AITestRunStatus.Queued, collection: CollectionName)
            .OrderBy(index => index.QueuedUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return runs.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AITestRun>> ListByBatchAsync(string batchId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(batchId))
        {
            return [];
        }

        var runs = await Session.Query<AITestRun, AITestRunIndex>(index => index.BatchId == batchId, collection: CollectionName)
            .OrderBy(index => index.QueuedUtc)
            .ListAsync(cancellationToken);

        return runs.ToList();
    }
}

/// <summary>
/// Stores QA cohorts.
/// </summary>
public interface IAIQACohortStore : ICatalog<AIQACohort>
{
    /// <summary>
    /// Lists the cohorts that score conversations.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cohorts.</returns>
    Task<IReadOnlyList<AIQACohort>> ListEnabledAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores QA cohorts in the AI collection.
/// </summary>
public sealed class AIQACohortStore : DocumentCatalog<AIQACohort, AIQACohortIndex>, IAIQACohortStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIQACohortStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIQACohortStore(ISession session, IOptions<YesSqlStoreOptions> options)
        : base(session)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIQACohort>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var cohorts = await Session.Query<AIQACohort, AIQACohortIndex>(index => index.Enabled, collection: CollectionName)
            .ListAsync(cancellationToken);

        return cohorts.ToList();
    }
}

/// <summary>
/// Stores AI alert rules.
/// </summary>
public interface IAIAlertRuleStore : ICatalog<AIAlertRule>
{
    /// <summary>
    /// Lists the rules that are checked.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rules.</returns>
    Task<IReadOnlyList<AIAlertRule>> ListEnabledAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores AI alert rules in the AI collection.
/// </summary>
public sealed class AIAlertRuleStore : DocumentCatalog<AIAlertRule, AIAlertRuleIndex>, IAIAlertRuleStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIAlertRuleStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIAlertRuleStore(ISession session, IOptions<YesSqlStoreOptions> options)
        : base(session)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIAlertRule>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var rules = await Session.Query<AIAlertRule, AIAlertRuleIndex>(index => index.Enabled, collection: CollectionName)
            .ListAsync(cancellationToken);

        return rules.ToList();
    }
}
