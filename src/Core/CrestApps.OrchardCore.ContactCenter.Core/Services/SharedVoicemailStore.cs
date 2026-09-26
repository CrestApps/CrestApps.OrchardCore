using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="ISharedVoicemailStore"/>.
/// </summary>
public sealed class SharedVoicemailStore : DocumentCatalog<SharedVoicemail, SharedVoicemailIndex>, ISharedVoicemailStore
{
    private const int MaxPageSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedVoicemailStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SharedVoicemailStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    // Two teammates claiming the same message at once must not both be told it is theirs: the second save fails
    // instead of silently overwriting the first claim.
    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <inheritdoc/>
    public async Task<SharedVoicemail> FindByInteractionIdAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        var voicemail = await Session.Query<SharedVoicemail, SharedVoicemailIndex>(
            index => index.InteractionId == interactionId,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        return await LoadedAsync(voicemail);
    }

    /// <inheritdoc/>
    public async Task<SharedVoicemailPage> QueryAsync(SharedVoicemailQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queueIds = query.QueueIds?
            .Where(queueId => !string.IsNullOrEmpty(queueId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // A caller entitled to no queue reads nothing. An empty IN list is not valid SQL, so answer without asking.
        if (queueIds is { Length: 0 })
        {
            return SharedVoicemailPage.Empty;
        }

        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var page = Math.Max(1, query.Page);

        var voicemails = Session.Query<SharedVoicemail, SharedVoicemailIndex>(collection: ContactCenterStorage.CollectionName);

        if (queueIds is not null)
        {
            voicemails = voicemails.Where(index => index.QueueId.IsIn(queueIds));
        }

        if (query.Status is { } status)
        {
            voicemails = voicemails.Where(index => index.Status == status);
        }
        else if (!query.IncludeResolved)
        {
            voicemails = voicemails.Where(index => index.Status != SharedVoicemailStatus.Resolved);
        }

        var ordered = voicemails
            .OrderByDescending(index => index.ReceivedUtc)
            .ThenByDescending(index => index.DocumentId);

        var count = await ordered.CountAsync(cancellationToken);
        var entries = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync(cancellationToken);

        return new SharedVoicemailPage
        {
            Count = count,
            Entries = await LoadedAsync(entries),
        };
    }
}
