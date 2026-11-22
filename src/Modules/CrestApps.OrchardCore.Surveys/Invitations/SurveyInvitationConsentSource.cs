using CrestApps.OrchardCore.Consent;

namespace CrestApps.OrchardCore.Surveys.Invitations;

/// <summary>
/// The unsubscribes of survey invitations, as a consent source: every send that asks the consent service, survey
/// invitations and Outreach runs alike, skips an address that unsubscribed from survey invitations. It records only the
/// changes made by the unsubscribe link of an invitation.
/// </summary>
internal sealed class SurveyInvitationConsentSource : IConsentSource
{
    /// <summary>
    /// The name of the source, and the source of the changes the unsubscribe link records.
    /// </summary>
    public const string SourceName = "survey-invitations";

    private readonly SurveyInvitationOptOutStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationConsentSource"/> class.
    /// </summary>
    /// <param name="store">The opt-out store.</param>
    public SurveyInvitationConsentSource(SurveyInvitationOptOutStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public string Name
        => SourceName;

    /// <inheritdoc />
    /// <exception cref="SurveyInvitationOptOutUnavailableException">The opt-out list's key can't be read, so the source
    /// can't tell. The consent contract has no "unknown" answer, so the source fails the question rather than saying the
    /// person opted out (which would block every e-mail on the site) or did not (which could mail someone who
    /// unsubscribed); the send that asked fails and is retried later.</exception>
    public async Task<bool> HasOptedOutAsync(ConsentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!string.Equals(query.Channel, ConsentChannels.Email, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(query.Address))
        {
            return false;
        }

        return await _store.GetStateAsync(query.Address, cancellationToken) switch
        {
            SurveyInvitationOptOutState.OptedOut => true,
            SurveyInvitationOptOutState.Unknown => throw new SurveyInvitationOptOutUnavailableException(),
            _ => false,
        };
    }

    /// <inheritdoc />
    public Task RecordAsync(string channel, string address, ConsentChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (!string.Equals(channel, ConsentChannels.Email, StringComparison.Ordinal)
            || !string.Equals(change.Source, SourceName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        return _store.SetAsync(address, change.OptedOut, cancellationToken);
    }
}
