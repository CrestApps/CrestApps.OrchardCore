using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="ICallRecordingStore"/>.
/// </summary>
public sealed class CallRecordingStore : DocumentCatalog<CallRecording, CallRecordingIndex>, ICallRecordingStore
{
    private const int MaxPageSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallRecordingStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public CallRecordingStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<CallRecording> FindByProviderRecordingIdAsync(string providerRecordingId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerRecordingId);

        var recording = await Session.Query<CallRecording, CallRecordingIndex>(
            index => index.ProviderRecordingId == providerRecordingId,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        return await LoadedAsync(recording);
    }

    /// <inheritdoc/>
    public async Task<CallRecording> FindRunningByProviderCallIdAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerCallId);

        var recording = await Session.Query<CallRecording, CallRecordingIndex>(
            index => index.ProviderCallId == providerCallId && index.ProviderRecordingId == null,
            collection: ContactCenterStorage.CollectionName)
            .OrderByDescending(index => index.StartedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return await LoadedAsync(recording);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CallRecording>> ListByInteractionIdAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        var recordings = await Session.Query<CallRecording, CallRecordingIndex>(
            index => index.InteractionId == interactionId,
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.StartedUtc)
            .ListAsync(cancellationToken);

        return (await LoadedAsync(recordings)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<CallRecordingPage> QueryAsync(CallRecordingQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var page = Math.Max(1, query.Page);

        var recordings = Session.Query<CallRecording, CallRecordingIndex>(
            index => index.IsStored && !index.IsErased,
            collection: ContactCenterStorage.CollectionName);

        if (!string.IsNullOrEmpty(query.AgentUserId))
        {
            var agentUserId = query.AgentUserId;

            recordings = recordings.Where(index => index.AgentUserId == agentUserId);
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerAddress))
        {
            // Numbers are stored as dialed (usually E.164), so a search for the local digits still matches.
            var address = query.CustomerAddress.Trim();

            recordings = recordings.Where(index => index.CustomerAddress.Contains(address));
        }

        if (query.Direction is { } direction)
        {
            recordings = recordings.Where(index => index.Direction == direction);
        }

        if (query.Source is { } source)
        {
            recordings = recordings.Where(index => index.Source == source);
        }

        if (query.FromUtc is { } fromUtc)
        {
            recordings = recordings.Where(index => index.StartedUtc >= fromUtc);
        }

        if (query.ToUtc is { } toUtc)
        {
            recordings = recordings.Where(index => index.StartedUtc < toUtc);
        }

        var ordered = recordings
            .OrderByDescending(index => index.StartedUtc)
            .ThenByDescending(index => index.DocumentId);

        var count = await ordered.CountAsync(cancellationToken);
        var entries = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync(cancellationToken);

        return new CallRecordingPage
        {
            Count = count,
            Entries = (await LoadedAsync(entries)).ToArray(),
        };
    }
}
