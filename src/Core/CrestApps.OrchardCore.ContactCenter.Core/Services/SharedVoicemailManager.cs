using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="ISharedVoicemailManager"/>.
/// </summary>
public sealed class SharedVoicemailManager : CatalogManager<SharedVoicemail>, ISharedVoicemailManager
{
    private readonly ISharedVoicemailStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedVoicemailManager"/> class.
    /// </summary>
    /// <param name="store">The underlying shared voicemail store.</param>
    /// <param name="handlers">The catalog entry handlers for shared voicemails.</param>
    /// <param name="logger">The logger instance.</param>
    public SharedVoicemailManager(
        ISharedVoicemailStore store,
        IEnumerable<ICatalogEntryHandler<SharedVoicemail>> handlers,
        ILogger<CatalogManager<SharedVoicemail>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<SharedVoicemail> FindByInteractionIdAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        var voicemail = await _store.FindByInteractionIdAsync(interactionId, cancellationToken);

        if (voicemail is not null)
        {
            await LoadAsync(voicemail, cancellationToken);
        }

        return voicemail;
    }

    /// <inheritdoc/>
    public async Task<SharedVoicemailPage> QueryAsync(SharedVoicemailQuery query, CancellationToken cancellationToken = default)
    {
        var page = await _store.QueryAsync(query, cancellationToken);

        foreach (var voicemail in page.Entries)
        {
            await LoadAsync(voicemail, cancellationToken);
        }

        return page;
    }
}
