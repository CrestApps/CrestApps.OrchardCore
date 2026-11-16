using CrestApps.OrchardCore.Learning.Xapi.Core.Indexes;
using CrestApps.OrchardCore.Learning.Xapi.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Services;

/// <summary>
/// What a credential may read: every statement, or only those of one actor and registration (a launch token), or only
/// those it stored ("read own").
/// </summary>
internal sealed record XapiReadScope(string ActorHash, string Registration, string AuthorityHash)
{
    public static XapiReadScope All { get; } = new(null, null, null);
}

/// <summary>
/// Stores statements in their collection and runs the statement queries on the indexes.
/// </summary>
public sealed class XapiStatementStore : DocumentCatalog<XapiStatementRecord, XapiStatementIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XapiStatementStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public XapiStatementStore(ISession session)
        : base(session)
    {
        CollectionName = XapiConstants.CollectionName;
    }

    /// <summary>
    /// Finds a statement by its id.
    /// </summary>
    /// <param name="statementId">The statement id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The record, or <see langword="null"/>.</returns>
    public async Task<XapiStatementRecord> FindByStatementIdAsync(string statementId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(statementId))
        {
            return null;
        }

        var id = statementId.ToLowerInvariant();

        return await Session.Query<XapiStatementRecord, XapiStatementIndex>(index => index.StatementId == id, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists the statements with the given ids.
    /// </summary>
    /// <param name="statementIds">The ids, in lower case.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The records found.</returns>
    public async Task<IReadOnlyList<XapiStatementRecord>> ListByStatementIdsAsync(IReadOnlyCollection<string> statementIds, CancellationToken cancellationToken = default)
    {
        if (statementIds.Count == 0)
        {
            return [];
        }

        var found = new List<XapiStatementRecord>();

        // In chunks, so a large batch stays within the parameters a database allows.
        foreach (var chunk in statementIds.Chunk(200))
        {
            found.AddRange(await Session.Query<XapiStatementRecord, XapiStatementIndex>(index => index.StatementId.IsIn(chunk), collection: CollectionName)
                .ListAsync(cancellationToken));
        }

        return found;
    }

    /// <summary>
    /// Lists the statements whose object refers to a statement.
    /// </summary>
    /// <param name="targetStatementId">The referred statement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The referring statements.</returns>
    public async Task<IReadOnlyList<XapiStatementRecord>> ListReferringAsync(string targetStatementId, CancellationToken cancellationToken = default)
        => (await Session.Query<XapiStatementRecord, XapiStatementIndex>(index => index.TargetStatementId == targetStatementId, collection: CollectionName)
            .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Lists the statements whose actor is one of the given agents, for personal data.
    /// </summary>
    /// <param name="actorKeys">The agent keys.</param>
    /// <param name="accountName">An account name to match whatever the home page, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The statements, oldest first.</returns>
    public async Task<IReadOnlyList<XapiStatementRecord>> ListByActorAsync(IEnumerable<string> actorKeys, string accountName, CancellationToken cancellationToken = default)
    {
        var hashes = actorKeys.Where(key => !string.IsNullOrEmpty(key)).Select(XapiAgents.Hash).Distinct(StringComparer.Ordinal).ToArray();

        if (hashes.Length == 0 && string.IsNullOrEmpty(accountName))
        {
            return [];
        }

        var query = string.IsNullOrEmpty(accountName)
            ? Session.Query<XapiStatementRecord, XapiStatementIndex>(index => index.ActorHash.IsIn(hashes), collection: CollectionName)
            : Session.Query<XapiStatementRecord, XapiStatementIndex>(index => index.ActorHash.IsIn(hashes) || index.ActorAccountName == accountName, collection: CollectionName);

        return (await query.OrderBy(index => index.Sequence).ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Reads a page of statements, oldest first, for an export.
    /// </summary>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page.</returns>
    public async Task<IReadOnlyList<XapiStatementRecord>> PageForExportAsync(int skip, int take, CancellationToken cancellationToken = default)
        => (await Session.Query<XapiStatementRecord, XapiStatementIndex>(collection: CollectionName)
            .OrderBy(index => index.Sequence)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Max(take, 1))
            .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Gets the position of the last statement stored, in the order statements were stored in.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The position, or 0 before the first statement.</returns>
    public async Task<long> CurrentSequenceAsync(CancellationToken cancellationToken = default)
    {
        var last = await Session.QueryIndex<XapiStatementIndex>(collection: CollectionName)
            .OrderByDescending(index => index.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

        return last?.Sequence ?? 0;
    }

    /// <summary>
    /// Reads a page of statements for the admin list, newest first, voided ones included.
    /// </summary>
    /// <param name="filter">The filters.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many match, and the page.</returns>
    public async Task<(int Count, IReadOnlyList<XapiStatementRecord> Items)> PageForAdminAsync(XapiStatementAdminFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Session.Query<XapiStatementRecord, XapiStatementIndex>(collection: CollectionName);

        if (!string.IsNullOrEmpty(filter.Verb))
        {
            var hash = XapiAgents.Hash(filter.Verb);

            query = query.Where(index => index.VerbHash == hash);
        }

        if (!string.IsNullOrEmpty(filter.Activity))
        {
            var hash = XapiAgents.Hash(filter.Activity);

            query = query.Where(index => index.ObjectHash == hash);
        }

        if (!string.IsNullOrEmpty(filter.Registration))
        {
            var registration = filter.Registration.ToLowerInvariant();

            query = query.Where(index => index.Registration == registration);
        }

        if (filter.ActorKeys.Count > 0 || !string.IsNullOrEmpty(filter.ActorAccountName))
        {
            var hashes = filter.ActorKeys.Select(XapiAgents.Hash).ToArray();
            var accountName = filter.ActorAccountName ?? string.Empty;

            query = query.Where(index => index.ActorHash.IsIn(hashes) || index.ActorAccountName == accountName);
        }

        if (filter.FromUtc is { } from)
        {
            query = query.Where(index => index.StoredUtc >= from);
        }

        if (filter.ToUtc is { } to)
        {
            query = query.Where(index => index.StoredUtc < to);
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(index => index.Sequence)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Max(take, 1))
            .ListAsync(cancellationToken);

        return (count, items.ToArray());
    }

    internal async Task<XapiQueryResult> QueryAsync(XapiStatementQuery filter, XapiReadScope scope, CancellationToken cancellationToken = default)
    {
        IQuery<XapiStatementRecord> query = Session.Query<XapiStatementRecord>(collection: CollectionName);

        if (!string.IsNullOrEmpty(filter.AgentKey))
        {
            var hash = XapiAgents.Hash(filter.AgentKey);

            query = filter.RelatedAgents
                ? query.With<XapiStatementAgentIndex>(index => index.KeyHash == hash)
                : query.With<XapiStatementAgentIndex>(index => index.KeyHash == hash && index.Direct == true);
        }

        if (!string.IsNullOrEmpty(filter.Activity))
        {
            var hash = XapiAgents.Hash(filter.Activity);

            query = filter.RelatedActivities
                ? query.With<XapiStatementActivityIndex>(index => index.ActivityHash == hash)
                : query.With<XapiStatementActivityIndex>(index => index.ActivityHash == hash && index.Direct == true);
        }

        if (!string.IsNullOrEmpty(filter.Verb))
        {
            var hash = XapiAgents.Hash(filter.Verb);

            query = query.With<XapiStatementVerbIndex>(index => index.VerbHash == hash);
        }

        if (!string.IsNullOrEmpty(filter.Registration))
        {
            var registration = filter.Registration.ToLowerInvariant();

            query = query.With<XapiStatementRegistrationIndex>(index => index.Registration == registration);
        }

        var statements = query.With<XapiStatementIndex>(index => index.Voided == false);

        if (filter.SinceUtc is { } since)
        {
            statements = statements.Where(index => index.StoredUtc > since);
        }

        if (filter.UntilUtc is { } until)
        {
            statements = statements.Where(index => index.StoredUtc <= until);
        }

        if (filter.MaxSequence is { } maxSequence)
        {
            statements = statements.Where(index => index.Sequence <= maxSequence);
        }

        if (!string.IsNullOrEmpty(scope.ActorHash))
        {
            statements = statements.Where(index => index.ActorHash == scope.ActorHash);
        }

        if (!string.IsNullOrEmpty(scope.Registration))
        {
            statements = statements.Where(index => index.Registration == scope.Registration);
        }

        if (!string.IsNullOrEmpty(scope.AuthorityHash))
        {
            statements = statements.Where(index => index.AuthorityHash == scope.AuthorityHash);
        }

        // The order statements were stored in: the sequence follows the stored time, since writes are serialized.
        var ordered = filter.Ascending
            ? statements.OrderBy(index => index.Sequence)
            : statements.OrderByDescending(index => index.Sequence);

        var limit = Math.Max(filter.Limit, 1);
        var page = (await ordered.Skip(Math.Max(filter.Skip, 0)).Take(limit + 1).ListAsync(cancellationToken)).ToList();

        return new XapiQueryResult
        {
            Records = page.Take(limit).ToArray(),
            HasMore = page.Count > limit,
        };
    }
}
