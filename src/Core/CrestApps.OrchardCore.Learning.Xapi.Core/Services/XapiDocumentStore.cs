using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core.Indexes;
using CrestApps.OrchardCore.Learning.Xapi.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Services;

/// <summary>
/// The address of a document: its kind, activity, agent, registration and id.
/// </summary>
/// <param name="Kind">The kind, one of <see cref="XapiDocumentKinds"/>.</param>
/// <param name="ActivityId">The activity id, or <see langword="null"/> for an agent profile.</param>
/// <param name="AgentKey">The agent key, or <see langword="null"/> for an activity profile.</param>
/// <param name="Registration">The registration of a state, in lower case, or <see langword="null"/>.</param>
public sealed record XapiDocumentScope(string Kind, string ActivityId, string AgentKey, string Registration);

/// <summary>
/// Stores documents in their collection.
/// </summary>
public sealed class XapiDocumentStore : DocumentCatalog<XapiDocument, XapiDocumentIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XapiDocumentStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public XapiDocumentStore(ISession session)
        : base(session)
    {
        CollectionName = XapiConstants.CollectionName;
    }

    /// <summary>
    /// Finds a document.
    /// </summary>
    /// <param name="scope">Where the document is.</param>
    /// <param name="documentId">The state id or profile id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The document, or <see langword="null"/>.</returns>
    public async Task<XapiDocument> FindAsync(XapiDocumentScope scope, string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var hash = XapiAgents.Hash(documentId);

        return (await Query(scope).Where(index => index.DocumentHash == hash).ListAsync(cancellationToken))
            .FirstOrDefault(document => Matches(document, scope) && string.Equals(document.DocumentId, documentId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Stores a document a module writes itself, such as the cmi5 launch data: it replaces the document's content.
    /// </summary>
    /// <param name="scope">Where the document is.</param>
    /// <param name="documentId">The state id or profile id.</param>
    /// <param name="agent">The agent, as JSON, or <see langword="null"/> for an activity profile.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="content">The content.</param>
    /// <param name="nowUtc">The time, in UTC.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The document.</returns>
    public async Task<XapiDocument> SaveAsync(XapiDocumentScope scope, string documentId, string agent, string contentType, byte[] content, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrEmpty(documentId);

        var document = await FindAsync(scope, documentId, cancellationToken);
        var isNew = document is null;

        document ??= new XapiDocument
        {
            Kind = scope.Kind,
            ActivityId = scope.ActivityId,
            AgentKey = scope.AgentKey,
            Agent = agent,
            Registration = scope.Registration,
            DocumentId = documentId,
        };

        document.ContentType = contentType;
        document.Content = content ?? [];
        document.ETag = XapiDocumentsResource.ETagOf(document.Content);
        document.UpdatedUtc = nowUtc;

        if (isNew)
        {
            await CreateAsync(document, cancellationToken);
        }
        else
        {
            await UpdateAsync(document, cancellationToken);
        }

        return document;
    }

    /// <summary>
    /// Lists the documents in a scope, changed after a time.
    /// </summary>
    /// <param name="scope">Where the documents are.</param>
    /// <param name="sinceUtc">The time after which the documents changed, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The documents.</returns>
    public async Task<IReadOnlyList<XapiDocument>> ListAsync(XapiDocumentScope scope, DateTime? sinceUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var query = Query(scope);

        if (sinceUtc is { } since)
        {
            query = query.Where(index => index.UpdatedUtc > since);
        }

        return (await query.OrderBy(index => index.Id).ListAsync(cancellationToken)).Where(document => Matches(document, scope)).ToArray();
    }

    /// <summary>
    /// Lists the documents of agents, for personal data: by agent key, and by account name on any home page.
    /// </summary>
    /// <param name="agentKeys">The agent keys.</param>
    /// <param name="accountNames">The account names, matched whatever the home page of the account.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The documents.</returns>
    public async Task<IReadOnlyList<XapiDocument>> ListByAgentsAsync(IEnumerable<string> agentKeys, IEnumerable<string> accountNames = null, CancellationToken cancellationToken = default)
    {
        var hashes = agentKeys.Where(key => !string.IsNullOrEmpty(key)).Select(XapiAgents.Hash).Distinct(StringComparer.Ordinal).ToArray();
        var names = (accountNames ?? []).Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.Ordinal).ToArray();

        if (hashes.Length == 0 && names.Length == 0)
        {
            return [];
        }

        var query = names.Length == 0
            ? Session.Query<XapiDocument, XapiDocumentIndex>(index => index.AgentHash.IsIn(hashes), collection: CollectionName)
            : hashes.Length == 0
                ? Session.Query<XapiDocument, XapiDocumentIndex>(index => index.AccountName.IsIn(names), collection: CollectionName)
                : Session.Query<XapiDocument, XapiDocumentIndex>(index => index.AgentHash.IsIn(hashes) || index.AccountName.IsIn(names), collection: CollectionName);

        return (await query.ListAsync(cancellationToken)).ToArray();
    }

    private IQuery<XapiDocument, XapiDocumentIndex> Query(XapiDocumentScope scope)
    {
        var kind = scope.Kind;
        var activityHash = XapiAgents.Hash(scope.ActivityId);
        var agentHash = XapiAgents.Hash(scope.AgentKey);
        var query = Session.Query<XapiDocument, XapiDocumentIndex>(index => index.Kind == kind, collection: CollectionName);

        if (activityHash is not null)
        {
            query = query.Where(index => index.ActivityHash == activityHash);
        }

        if (agentHash is not null)
        {
            query = query.Where(index => index.AgentHash == agentHash);
        }

        if (kind == XapiDocumentKinds.State)
        {
            var registration = scope.Registration;

            query = registration is null
                ? query.Where(index => index.Registration == null)
                : query.Where(index => index.Registration == registration);
        }

        return query;
    }

    private static bool Matches(XapiDocument document, XapiDocumentScope scope)
        => string.Equals(document.Kind, scope.Kind, StringComparison.Ordinal) &&
            (scope.ActivityId is null || string.Equals(document.ActivityId, scope.ActivityId, StringComparison.Ordinal)) &&
            (scope.AgentKey is null || string.Equals(document.AgentKey, scope.AgentKey, StringComparison.Ordinal));
}

/// <summary>
/// Stores the canonical definitions of activities.
/// </summary>
public sealed class XapiActivityStore : DocumentCatalog<XapiActivityRecord, XapiActivityIndex>
{
    private readonly Dictionary<string, XapiActivityRecord> _pending = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="XapiActivityStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public XapiActivityStore(ISession session)
        : base(session)
    {
        CollectionName = XapiConstants.CollectionName;
    }

    /// <summary>
    /// Finds the canonical definition of an activity.
    /// </summary>
    /// <param name="activityId">The activity id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The record, or <see langword="null"/>.</returns>
    public async Task<XapiActivityRecord> FindAsync(string activityId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(activityId))
        {
            return null;
        }

        if (_pending.TryGetValue(activityId, out var pending))
        {
            return pending;
        }

        var hash = XapiAgents.Hash(activityId);

        return (await Session.Query<XapiActivityRecord, XapiActivityIndex>(index => index.ActivityHash == hash, collection: CollectionName).ListAsync(cancellationToken))
            .FirstOrDefault(record => string.Equals(record.ActivityId, activityId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Joins a definition a statement carried into the canonical one: language maps are joined language by
    /// language, and the other properties take the newer value.
    /// </summary>
    /// <param name="activityId">The activity id.</param>
    /// <param name="definition">The definition.</param>
    /// <param name="nowUtc">The time, in UTC.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task MergeAsync(string activityId, JsonObject definition, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var record = await FindAsync(activityId, cancellationToken);
        var isNew = record is null;
        var canonical = isNew ? new JsonObject() : JsonNode.Parse(record.Definition)?.AsObject() ?? [];

        foreach (var (name, value) in definition)
        {
            if (name is "name" or "description" && value is JsonObject map && canonical[name] is JsonObject existing)
            {
                foreach (var (language, text) in map)
                {
                    existing[language] = text?.DeepClone();
                }
            }
            else
            {
                canonical[name] = value?.DeepClone();
            }
        }

        record ??= new XapiActivityRecord { ActivityId = activityId };
        record.Definition = canonical.ToJsonString();
        record.UpdatedUtc = nowUtc;

        if (isNew)
        {
            _pending[activityId] = record;
            await CreateAsync(record, cancellationToken);
        }
        else
        {
            await UpdateAsync(record, cancellationToken);
        }
    }
}
