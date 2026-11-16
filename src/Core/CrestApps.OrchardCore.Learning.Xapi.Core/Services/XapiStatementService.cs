using System.Text.Json.Nodes;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Learning.Xapi.Core.Validation;
using CrestApps.OrchardCore.Learning.Xapi.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Services;

/// <summary>
/// Writes, reads and queries statements by the rules of xAPI 1.0.3: every statement is validated, a batch is stored
/// whole or not at all, a statement is immutable (the same id with the same statement changes nothing, with another
/// statement it is a conflict), voiding hides the voided statement from queries, <c>stored</c> and <c>authority</c>
/// are set by the LRS, attachments are matched to their content by SHA-2 and signed statements are checked. Writes
/// are serialized by one lock per tenant, taken only around the check and the insert, after every filter ran its
/// own checks, so a module that holds its own lock while it writes (cmi5) always takes its lock first.
/// </summary>
public sealed class XapiStatementService
{
    private const string LockKey = "Learning:Xapi:Statements";
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(60);
    private const int MaxReferenceDepth = 10;

    private readonly XapiStatementStore _store;
    private readonly XapiActivityStore _activities;
    private readonly ISession _session;
    private readonly IProtectedFileStore<XapiAttachmentFiles> _files;
    private readonly IEnumerable<IXapiStatementFilter> _filters;
    private readonly IEnumerable<IXapiStatementObserver> _observers;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly XapiOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="XapiStatementService"/> class.
    /// </summary>
    public XapiStatementService(
        XapiStatementStore store,
        XapiActivityStore activities,
        ISession session,
        IProtectedFileStore<XapiAttachmentFiles> files,
        IEnumerable<IXapiStatementFilter> filters,
        IEnumerable<IXapiStatementObserver> observers,
        IDistributedLock distributedLock,
        IClock clock,
        IOptions<XapiOptions> options,
        ILogger<XapiStatementService> logger)
    {
        _store = store;
        _activities = activities;
        _session = session;
        _files = files;
        _filters = filters;
        _observers = observers;
        _distributedLock = distributedLock;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Stores the statements a client sent, through the statement filters.
    /// </summary>
    /// <param name="statements">The statements, as parsed from the request.</param>
    /// <param name="attachments">The attachment parts of a <c>multipart/mixed</c> request.</param>
    /// <param name="credential">The credential of the request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was stored, or why not.</returns>
    public async Task<XapiWriteResult> WriteAsync(IReadOnlyList<JsonObject> statements, IReadOnlyList<XapiAttachmentContent> attachments, XapiCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(credential);

        if (!credential.CanWriteStatements && !credential.IsLaunch)
        {
            return XapiWriteResult.Fail(403, "The credential may not store statements.");
        }

        var prepared = await PrepareAsync(statements, attachments ?? [], credential.Launch, cancellationToken);

        if (prepared.Error is not null)
        {
            return prepared.Error;
        }

        var context = new XapiStatementWriteContext
        {
            Credential = credential,
            Statements = prepared.Statements,
            ExistingIds = prepared.Existing,
        };

        XapiWriteResult storeError = null;

        Func<Task> pipeline = async () =>
        {
            var stored = await StoreAsync(prepared, credential.Authority, credential.AuthorityKey, credential.ClientId, cancellationToken);

            if (stored.Error is not null)
            {
                storeError = stored.Error;

                return;
            }

            context.Stored = stored.Records;
        };

        foreach (var filter in _filters.Reverse())
        {
            var next = pipeline;
            var current = filter;

            pipeline = () => context.RejectedStatus is null && storeError is null ? current.OnWritingAsync(context, next) : Task.CompletedTask;
        }

        await pipeline();

        if (context.RejectedStatus is { } status)
        {
            return XapiWriteResult.Fail(status, context.RejectedMessage);
        }

        if (storeError is not null)
        {
            return storeError;
        }

        await NotifyAsync(context.Stored, credential);

        return new XapiWriteResult
        {
            Ids = prepared.Ids,
            Stored = context.Stored,
        };
    }

    /// <summary>
    /// Stores statements the LRS writes for a module, such as the cmi5 <c>launched</c> and <c>satisfied</c> statements:
    /// they are validated and stored with the authority given, and they do not pass the statement filters.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="authority">The authority.</param>
    /// <param name="authorityKey">The key of the authority.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was stored, or why not.</returns>
    public async Task<XapiWriteResult> WriteTrustedAsync(IReadOnlyList<JsonObject> statements, JsonObject authority, string authorityKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(authority);

        var prepared = await PrepareAsync(statements, [], launch: null, cancellationToken);

        if (prepared.Error is not null)
        {
            return prepared.Error;
        }

        var stored = await StoreAsync(prepared, authority, authorityKey, clientId: null, cancellationToken);

        if (stored.Error is not null)
        {
            return stored.Error;
        }

        await NotifyAsync(stored.Records, credential: null);

        return new XapiWriteResult
        {
            Ids = prepared.Ids,
            Stored = stored.Records,
        };
    }

    /// <summary>
    /// Finds a statement by its id for a credential: a voided statement only by <c>voidedStatementId</c>, and any other
    /// only by <c>statementId</c>.
    /// </summary>
    /// <param name="statementId">The statement id.</param>
    /// <param name="voided">Whether the statement was asked for by <c>voidedStatementId</c>.</param>
    /// <param name="credential">The credential.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The record, or <see langword="null"/> when there is none the credential may read.</returns>
    public async Task<XapiStatementRecord> FindAsync(string statementId, bool voided, XapiCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var scope = ScopeOf(credential);

        if (scope is null)
        {
            return null;
        }

        var record = await _store.FindByStatementIdAsync(statementId, cancellationToken);

        if (record is null || record.Voided != voided)
        {
            return null;
        }

        if ((scope.ActorHash is not null && XapiAgents.Hash(record.ActorKey) != scope.ActorHash) ||
            (scope.Registration is not null && record.Registration != scope.Registration) ||
            (scope.AuthorityHash is not null && XapiAgents.Hash(record.AuthorityKey) != scope.AuthorityHash))
        {
            return null;
        }

        return record;
    }

    /// <summary>
    /// Runs a statement query for a credential.
    /// </summary>
    /// <param name="query">The filters.</param>
    /// <param name="credential">The credential.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page, or <see langword="null"/> when the credential may not read statements.</returns>
    public async Task<XapiQueryResult> QueryAsync(XapiStatementQuery query, XapiCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(credential);

        var scope = ScopeOf(credential);

        if (scope is null)
        {
            return null;
        }

        var limit = query.Limit <= 0 ? _options.MaxQueryLimit : Math.Min(query.Limit, _options.MaxQueryLimit);

        query.Limit = limit;

        return await _store.QueryAsync(query, scope, cancellationToken);
    }

    /// <summary>
    /// Imports statements as they were stored on another site, such as from a recipe: each keeps its id, <c>stored</c>
    /// and <c>authority</c>, and its voided mark. A statement already here is skipped when it is the same and reported
    /// when it differs; an invalid statement is reported.
    /// </summary>
    /// <param name="items">The statements.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many were imported and skipped, and the errors.</returns>
    public async Task<XapiImportResult> ImportAsync(IReadOnlyList<XapiImportedStatement> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var result = new XapiImportResult();
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            result.Errors.Add("The Learning Record Store is busy. Try again.");

            return result;
        }

        await using (locker)
        {
            var sequence = await _store.CurrentSequenceAsync(cancellationToken);
            var batch = new Dictionary<string, XapiStatementRecord>(StringComparer.Ordinal);
            var records = new List<XapiStatementRecord>();

            foreach (var item in items)
            {
                var statement = item?.Statement?.DeepClone() as JsonObject;
                var error = statement is null ? "The statement is missing." : XapiStatementValidator.Validate(statement);

                if (error is null && (statement["id"] is null || statement["stored"] is null || statement["authority"] is null))
                {
                    error = "An imported statement needs its id, stored and authority.";
                }

                if (error is not null)
                {
                    result.Errors.Add(error);

                    continue;
                }

                XapiStatementNormalizer.Normalize(statement);

                var id = statement["id"].GetValue<string>();
                var existing = batch.GetValueOrDefault(id) ?? await _store.FindByStatementIdAsync(id, cancellationToken);

                if (existing is not null)
                {
                    if (XapiStatementNormalizer.AreEquivalent(statement, JsonNode.Parse(existing.Statement).AsObject()))
                    {
                        result.Skipped++;
                    }
                    else
                    {
                        result.Errors.Add($"The statement {id} is already stored with other content.");
                    }

                    continue;
                }

                // The validation above checked that stored is a timestamp.
                _ = XapiFormats.TryParseTimestamp(statement["stored"].GetValue<string>(), out var stored);
                statement["version"] ??= XapiConstants.DefaultStatementVersion;
                statement["timestamp"] ??= statement["stored"].DeepClone();

                var record = await BuildRecordAsync(statement, XapiFormats.ToMilliseconds(stored), item.AuthorityKey, item.ClientId, [], cancellationToken);

                record.Sequence = ++sequence;
                record.Voided = item.Voided && !record.IsVoiding;

                if (record.TargetStatementId is not null)
                {
                    var target = batch.GetValueOrDefault(record.TargetStatementId) ?? await _store.FindByStatementIdAsync(record.TargetStatementId, cancellationToken);

                    record.Keys = XapiStatementNormalizer.Join(record.OwnKeys, target?.Keys);

                    if (record.IsVoiding && target is not null && !target.IsVoiding && !target.Voided)
                    {
                        target.Voided = true;
                        target.VoidedBy = record.StatementId;

                        if (!batch.ContainsKey(target.StatementId))
                        {
                            await _store.UpdateAsync(target, cancellationToken);
                        }
                    }
                }

                batch[id] = record;
                records.Add(record);
                await _store.CreateAsync(record, cancellationToken);

                foreach (var (activityId, definition) in DefinitionsOf(statement))
                {
                    await _activities.MergeAsync(activityId, definition, record.StoredUtc, cancellationToken);
                }
            }

            foreach (var record in records)
            {
                await PropagateKeysAsync(record, batch, depth: 0, cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);
            result.Imported = records.Count;
        }

        return result;
    }

    /// <summary>
    /// Deletes statements and the content of their attachments, as personal data erasure asks. A statement that refers
    /// to a deleted one keeps its own keys.
    /// </summary>
    /// <param name="records">The statements.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task DeleteAsync(IReadOnlyList<XapiStatementRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        foreach (var record in records)
        {
            foreach (var file in record.Attachments ?? [])
            {
                await _files.DeleteAsync(file.FileId, cancellationToken);
            }

            await _store.DeleteAsync(record, cancellationToken);
        }

        await _session.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the position of the last statement stored, which fixes the end of a query's pages.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The position.</returns>
    public Task<long> CurrentSequenceAsync(CancellationToken cancellationToken = default)
        => _store.CurrentSequenceAsync(cancellationToken);

    /// <summary>
    /// Opens the content of an attachment.
    /// </summary>
    /// <param name="file">The attachment file.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The content, or <see langword="null"/> when it is gone.</returns>
    public Task<Stream> OpenAttachmentAsync(XapiAttachmentFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        return _files.OpenAsync(file.FileId, cancellationToken);
    }

    /// <summary>
    /// Gets whether a credential may read statements at all.
    /// </summary>
    /// <param name="credential">The credential.</param>
    /// <returns><see langword="true"/> when it may read some statements.</returns>
    public static bool CanRead(XapiCredential credential)
        => credential is not null && (credential.CanReadAllStatements || credential.CanReadOwnStatements || credential.IsLaunch);

    internal static XapiReadScope ScopeOf(XapiCredential credential)
    {
        if (credential.CanReadAllStatements)
        {
            return XapiReadScope.All;
        }

        if (credential.IsLaunch)
        {
            return new XapiReadScope(XapiAgents.Hash(credential.Launch.ActorKey), credential.Launch.Registration?.ToLowerInvariant(), null);
        }

        return credential.CanReadOwnStatements
            ? new XapiReadScope(null, null, XapiAgents.Hash(credential.AuthorityKey))
            : null;
    }

    private async Task<PreparedWrite> PrepareAsync(IReadOnlyList<JsonObject> statements, IReadOnlyList<XapiAttachmentContent> attachments, XapiLaunchScope launch, CancellationToken cancellationToken)
    {
        if (statements.Count == 0)
        {
            return PreparedWrite.Fail(400, "No statement was sent.");
        }

        if (statements.Count > _options.MaxStatementsPerRequest)
        {
            return PreparedWrite.Fail(413, $"A request may store at most {_options.MaxStatementsPerRequest} statements.");
        }

        var prepared = new List<JsonObject>(statements.Count);
        var ids = new List<string>(statements.Count);

        for (var index = 0; index < statements.Count; index++)
        {
            var statement = statements[index]?.DeepClone() as JsonObject;

            if (statement is null)
            {
                return PreparedWrite.Fail(400, $"Statement {index + 1} is not a JSON object.");
            }

            if (System.Text.Encoding.UTF8.GetByteCount(statement.ToJsonString()) > _options.MaxStatementBytes)
            {
                return PreparedWrite.Fail(413, $"Statement {index + 1} is larger than {_options.MaxStatementBytes} bytes.");
            }

            var error = XapiStatementValidator.Validate(statement);

            if (error is not null)
            {
                return PreparedWrite.Fail(400, $"Statement {index + 1} is not valid: {error}");
            }

            if (launch is not null)
            {
                var registration = XapiAgents.Text(statement["context"]?["registration"]);

                if (!string.Equals(XapiAgents.Key(statement["actor"]), launch.ActorKey, StringComparison.Ordinal) ||
                    !string.Equals(registration, launch.Registration, StringComparison.OrdinalIgnoreCase))
                {
                    return PreparedWrite.Fail(403, $"Statement {index + 1} is not about the actor and registration of the launch.");
                }
            }

            XapiStatementNormalizer.Normalize(statement);

            if (statement["id"] is null)
            {
                statement["id"] = Guid.NewGuid().ToString();
            }

            var id = statement["id"].GetValue<string>();

            if (ids.Contains(id, StringComparer.Ordinal))
            {
                return PreparedWrite.Fail(400, $"The statement id {id} is in the request twice.");
            }

            ids.Add(id);
            prepared.Add(statement);
        }

        var attachmentError = CheckAttachments(prepared, attachments);

        if (attachmentError is not null)
        {
            return PreparedWrite.Fail(attachmentError.Value.Status, attachmentError.Value.Message);
        }

        var existing = await _store.ListByStatementIdsAsync(ids, cancellationToken);
        var existingIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var record in existing)
        {
            var sent = prepared.First(statement => statement["id"].GetValue<string>() == record.StatementId);

            if (!XapiStatementNormalizer.AreEquivalent(sent, JsonNode.Parse(record.Statement).AsObject()))
            {
                return PreparedWrite.Fail(409, $"The statement {record.StatementId} is already stored with other content; a statement cannot change.");
            }

            existingIds.Add(record.StatementId);
        }

        // A voiding statement cannot be voided.
        var voidingInRequest = prepared.Where(IsVoiding).Select(statement => statement["id"].GetValue<string>()).ToHashSet(StringComparer.Ordinal);

        foreach (var statement in prepared.Where(statement => IsVoiding(statement) && !existingIds.Contains(statement["id"].GetValue<string>())))
        {
            var targetId = statement["object"]["id"].GetValue<string>();

            if (voidingInRequest.Contains(targetId) || (await _store.FindByStatementIdAsync(targetId, cancellationToken))?.IsVoiding == true)
            {
                return PreparedWrite.Fail(400, $"The statement {targetId} voids another statement, and a voiding statement cannot be voided.");
            }
        }

        return new PreparedWrite
        {
            Statements = prepared,
            Ids = ids,
            Existing = existingIds,
            Attachments = attachments,
        };
    }

    private static (int Status, string Message)? CheckAttachments(List<JsonObject> statements, IReadOnlyList<XapiAttachmentContent> parts)
    {
        var byHash = new Dictionary<string, XapiAttachmentContent>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part.Sha2) || !XapiFormats.IsSha2(part.Sha2))
            {
                return (400, "Every attachment part needs an X-Experience-API-Hash header with its SHA-2 hash.");
            }

            if (!string.Equals(XapiMultipart.Sha2(part.Content, part.Sha2.Length), part.Sha2, StringComparison.OrdinalIgnoreCase))
            {
                return (400, $"The content of the attachment part {part.Sha2} does not have that SHA-2 hash.");
            }

            byHash.TryAdd(part.Sha2, part);
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var statement in statements)
        {
            foreach (var attachment in AttachmentsOf(statement))
            {
                var sha2 = attachment["sha2"].GetValue<string>();
                var hasContent = byHash.TryGetValue(sha2, out var part);
                var usage = attachment["usageType"].GetValue<string>();

                if (string.Equals(usage, XapiConstants.AttachmentUsages.Signature, StringComparison.Ordinal))
                {
                    if (!hasContent)
                    {
                        return (400, "A signed statement must send its signature as an attachment part.");
                    }

                    if (!string.Equals(XapiAgents.Text(attachment["contentType"]), "application/octet-stream", StringComparison.OrdinalIgnoreCase))
                    {
                        return (400, "The signature attachment must have the content type application/octet-stream.");
                    }

                    var error = XapiSignatures.Verify(statement, part.Content);

                    if (error is not null)
                    {
                        return (400, error);
                    }
                }

                if (!hasContent && attachment["fileUrl"] is null)
                {
                    return (400, $"The attachment {sha2} has no fileUrl and its content was not sent.");
                }

                if (hasContent)
                {
                    used.Add(sha2);
                }
            }
        }

        var extra = byHash.Keys.FirstOrDefault(sha2 => !used.Contains(sha2));

        return extra is null ? null : (400, $"The attachment part {extra} is not an attachment of any statement.");
    }

    private static IEnumerable<JsonObject> AttachmentsOf(JsonObject statement)
    {
        foreach (var attachment in statement["attachments"]?.AsArray() ?? [])
        {
            yield return attachment.AsObject();
        }

        if (statement["object"] is JsonObject sub && XapiStatementValidator.ObjectTypeOf(sub) == "SubStatement")
        {
            foreach (var attachment in sub["attachments"]?.AsArray() ?? [])
            {
                yield return attachment.AsObject();
            }
        }
    }

    private async Task<StoreResult> StoreAsync(PreparedWrite prepared, JsonObject authority, string authorityKey, string clientId, CancellationToken cancellationToken)
    {
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return StoreResult.Fail(503, "The Learning Record Store is busy. Try again.");
        }

        await using (locker)
        {
            // Checked again under the lock: another request may have stored one of these ids meanwhile.
            var existing = await _store.ListByStatementIdsAsync(prepared.Ids, cancellationToken);
            var existingIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var record in existing)
            {
                var sent = prepared.Statements.First(statement => statement["id"].GetValue<string>() == record.StatementId);

                if (!XapiStatementNormalizer.AreEquivalent(sent, JsonNode.Parse(record.Statement).AsObject()))
                {
                    return StoreResult.Fail(409, $"The statement {record.StatementId} is already stored with other content; a statement cannot change.");
                }

                existingIds.Add(record.StatementId);
            }

            var now = XapiFormats.ToMilliseconds(_clock.UtcNow);
            var storedText = XapiFormats.FormatTimestamp(now);
            var batch = new Dictionary<string, XapiStatementRecord>(StringComparer.Ordinal);
            var records = new List<XapiStatementRecord>();

            // The writes are serialized by the lock, so the next position is the last one plus one.
            var sequence = await _store.CurrentSequenceAsync(cancellationToken);

            foreach (var statement in prepared.Statements)
            {
                var id = statement["id"].GetValue<string>();

                if (existingIds.Contains(id))
                {
                    continue;
                }

                statement["stored"] = storedText;
                statement["authority"] = authority.DeepClone();
                statement["version"] ??= XapiConstants.DefaultStatementVersion;
                statement["timestamp"] ??= storedText;

                var record = await BuildRecordAsync(statement, now, authorityKey, clientId, prepared.Attachments, cancellationToken);

                record.Sequence = ++sequence;

                if (record.TargetStatementId is not null)
                {
                    var target = batch.GetValueOrDefault(record.TargetStatementId) ?? await _store.FindByStatementIdAsync(record.TargetStatementId, cancellationToken);

                    record.Keys = XapiStatementNormalizer.Join(record.OwnKeys, target?.Keys);

                    if (record.IsVoiding && target is not null && !target.IsVoiding)
                    {
                        target.Voided = true;
                        target.VoidedBy = record.StatementId;

                        if (!batch.ContainsKey(target.StatementId))
                        {
                            await _store.UpdateAsync(target, cancellationToken);
                        }
                    }
                }

                // A voiding statement that arrived before this one voids it now.
                var voiding = (await _store.ListReferringAsync(id, cancellationToken)).FirstOrDefault(referring => referring.IsVoiding)
                    ?? batch.Values.FirstOrDefault(other => other.IsVoiding && other.TargetStatementId == id);

                if (voiding is not null && !record.IsVoiding)
                {
                    record.Voided = true;
                    record.VoidedBy = voiding.StatementId;
                }

                batch[id] = record;
                records.Add(record);
            }

            foreach (var record in records)
            {
                await _store.CreateAsync(record, cancellationToken);
            }

            // The canonical definitions of the activities the statements describe.
            foreach (var statement in prepared.Statements.Where(statement => !existingIds.Contains(statement["id"].GetValue<string>())))
            {
                foreach (var (activityId, definition) in DefinitionsOf(statement))
                {
                    await _activities.MergeAsync(activityId, definition, now, cancellationToken);
                }
            }

            // Statements stored earlier that refer to these ones now match the filters of what they refer to.
            foreach (var record in records)
            {
                await PropagateKeysAsync(record, batch, depth: 0, cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);

            return new StoreResult { Records = records };
        }
    }

    private async Task PropagateKeysAsync(XapiStatementRecord target, Dictionary<string, XapiStatementRecord> batch, int depth, CancellationToken cancellationToken)
    {
        if (depth >= MaxReferenceDepth)
        {
            return;
        }

        foreach (var referring in await _store.ListReferringAsync(target.StatementId, cancellationToken))
        {
            if (batch.ContainsKey(referring.StatementId))
            {
                continue;
            }

            referring.Keys = XapiStatementNormalizer.Join(referring.OwnKeys, target.Keys);
            await _store.UpdateAsync(referring, cancellationToken);
            await PropagateKeysAsync(referring, batch, depth + 1, cancellationToken);
        }
    }

    private async Task<XapiStatementRecord> BuildRecordAsync(
        JsonObject statement,
        DateTime now,
        string authorityKey,
        string clientId,
        IReadOnlyList<XapiAttachmentContent> parts,
        CancellationToken cancellationToken)
    {
        var value = statement["object"].AsObject();
        var objectType = XapiStatementValidator.ObjectTypeOf(value);
        var account = statement["actor"]["account"] as JsonObject;
        var record = new XapiStatementRecord
        {
            StatementId = statement["id"].GetValue<string>(),
            StoredUtc = now,
            TimestampUtc = XapiFormats.TryParseTimestamp(XapiAgents.Text(statement["timestamp"]), out var timestamp) ? timestamp : now,
            Verb = statement["verb"]["id"].GetValue<string>(),
            ObjectType = objectType,
            ObjectId = objectType switch
            {
                "Agent" or "Group" => XapiAgents.Key(value),
                "SubStatement" => null,
                _ => XapiAgents.Text(value["id"]),
            },
            ActorKey = XapiAgents.Key(statement["actor"]),
            ActorAccountName = XapiAgents.Text(account?["name"]),
            ActorAccountHomePage = XapiAgents.Text(account?["homePage"]),
            Registration = XapiAgents.Text(statement["context"]?["registration"]),
            AuthorityKey = authorityKey,
            ClientId = clientId,
            IsVoiding = IsVoiding(statement),
            TargetStatementId = objectType == "StatementRef" ? XapiAgents.Text(value["id"]) : null,
            OwnKeys = XapiStatementNormalizer.BuildKeys(statement),
        };

        record.Keys = XapiStatementNormalizer.Join(record.OwnKeys, null);

        foreach (var attachment in AttachmentsOf(statement))
        {
            var sha2 = attachment["sha2"].GetValue<string>().ToLowerInvariant();
            var part = parts.FirstOrDefault(candidate => string.Equals(candidate.Sha2, sha2, StringComparison.OrdinalIgnoreCase));

            if (part is null || record.Attachments.Any(file => file.Sha2 == sha2))
            {
                continue;
            }

            // Each statement keeps its own copy, so erasing one statement never takes another's attachment.
            using var content = new MemoryStream(part.Content, writable: false);
            var saved = await _files.SaveAsync(sha2, content, cancellationToken);

            record.Attachments.Add(new XapiAttachmentFile
            {
                Sha2 = sha2,
                FileId = saved.FileId,
                ContentType = string.IsNullOrEmpty(part.ContentType) ? XapiAgents.Text(attachment["contentType"]) : part.ContentType,
                Length = part.Content.Length,
            });
        }

        record.Statement = statement.ToJsonString();

        return record;
    }

    private async Task NotifyAsync(IReadOnlyList<XapiStatementRecord> records, XapiCredential credential)
    {
        if (records.Count == 0)
        {
            return;
        }

        foreach (var observer in _observers)
        {
            try
            {
                await observer.StoredAsync(records, credential);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "A statement observer failed after {Count} statements were stored.", records.Count);
            }
        }
    }

    private static IEnumerable<(string ActivityId, JsonObject Definition)> DefinitionsOf(JsonObject statement)
    {
        var activities = new List<JsonNode>();

        void Collect(JsonObject body)
        {
            if (body?["object"] is JsonObject value && XapiStatementValidator.ObjectTypeOf(value) == "Activity")
            {
                activities.Add(value);
            }

            if (body?["context"]?["contextActivities"] is JsonObject contextActivities)
            {
                foreach (var (_, list) in contextActivities)
                {
                    activities.AddRange(list is JsonArray array ? array : [list]);
                }
            }
        }

        Collect(statement);

        if (statement["object"] is JsonObject sub && XapiStatementValidator.ObjectTypeOf(sub) == "SubStatement")
        {
            Collect(sub);
        }

        foreach (var activity in activities)
        {
            if (XapiAgents.Text(activity?["id"]) is { } id && activity["definition"] is JsonObject definition && definition.Count > 0)
            {
                yield return (id, definition);
            }
        }
    }

    private static bool IsVoiding(JsonObject statement)
        => string.Equals(XapiAgents.Text(statement["verb"]?["id"]), XapiConstants.Verbs.Voided, StringComparison.Ordinal);

    private sealed class PreparedWrite
    {
        public IReadOnlyList<JsonObject> Statements { get; init; } = [];

        public IReadOnlyList<string> Ids { get; init; } = [];

        public ISet<string> Existing { get; init; } = new HashSet<string>();

        public IReadOnlyList<XapiAttachmentContent> Attachments { get; init; } = [];

        public XapiWriteResult Error { get; init; }

        public static PreparedWrite Fail(int status, string message)
            => new() { Error = XapiWriteResult.Fail(status, message) };
    }

    private sealed class StoreResult
    {
        public IReadOnlyList<XapiStatementRecord> Records { get; init; } = [];

        public XapiWriteResult Error { get; init; }

        public static StoreResult Fail(int status, string message)
            => new() { Error = XapiWriteResult.Fail(status, message) };
    }
}
