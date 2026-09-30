using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Moves a converted lead's open voicemails to the contact it became, so the agent who returns the call works on the
/// contact. Resolved voicemails stay with the lead as part of its history.
/// </summary>
public sealed class VoicemailLeadConversionRepointer : ILeadConversionRepointer
{
    private static readonly SharedVoicemailStatus[] _openStatuses =
    [
        SharedVoicemailStatus.New,
        SharedVoicemailStatus.Claimed,
    ];

    private readonly ISession _session;
    private readonly ISharedVoicemailStore _store;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoicemailLeadConversionRepointer"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="store">The shared voicemail store.</param>
    /// <param name="logger">The logger.</param>
    public VoicemailLeadConversionRepointer(
        ISession session,
        ISharedVoicemailStore store,
        ILogger<VoicemailLeadConversionRepointer> logger)
    {
        _session = session;
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RepointAsync(LeadConversionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var leadId = context.Lead?.ContentItemId;
        var contactId = context.Contact?.ContentItemId;

        if (string.IsNullOrEmpty(leadId) || string.IsNullOrEmpty(contactId))
        {
            return;
        }

        // The voicemail index has no contact column, so the open voicemails are read by status and matched here.
        var voicemails = await _session.Query<SharedVoicemail, SharedVoicemailIndex>(
                index => index.Status.IsIn(_openStatuses),
                collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        var moved = 0;

        foreach (var voicemail in voicemails)
        {
            if (!string.Equals(voicemail.ContactContentItemId, leadId, StringComparison.Ordinal))
            {
                continue;
            }

            voicemail.ContactContentItemId = contactId;
            voicemail.ContactContentType = context.Contact.ContentType;

            await _store.UpdateAsync(voicemail, cancellationToken);
            moved++;
        }

        if (moved > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Moved {Count} open voicemail(s) of a converted lead to the contact it became.",
                moved);
        }
    }
}
