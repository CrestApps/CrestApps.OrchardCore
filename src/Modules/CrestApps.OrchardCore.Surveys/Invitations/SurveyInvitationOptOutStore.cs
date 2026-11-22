using CrestApps.OrchardCore.Surveys.Core.Access;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Invitations;

/// <summary>
/// Reads and changes the addresses that unsubscribed from survey invitations.
/// </summary>
internal sealed class SurveyInvitationOptOutStore
{
    private readonly ISession _session;

    private SurveyInvitationOptOutDocument _document;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationOptOutStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public SurveyInvitationOptOutStore(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Gets the hash an address is kept under.
    /// </summary>
    /// <param name="address">The e-mail address.</param>
    /// <returns>The hash, or <see langword="null"/> when there is no address.</returns>
    public static string HashOf(string address)
        => string.IsNullOrWhiteSpace(address) ? null : SurveyRespondentKeys.Hash(address.Trim().ToLowerInvariant());

    /// <summary>
    /// Gets whether an address unsubscribed.
    /// </summary>
    /// <param name="address">The e-mail address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when it unsubscribed.</returns>
    public async Task<bool> ContainsAsync(string address, CancellationToken cancellationToken = default)
    {
        var hash = HashOf(address);

        return hash is not null && (await LoadAsync(cancellationToken)).AddressHashes.Contains(hash);
    }

    /// <summary>
    /// Adds or removes an address.
    /// </summary>
    /// <param name="address">The e-mail address.</param>
    /// <param name="optedOut">Whether the address opted out.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SetAsync(string address, bool optedOut, CancellationToken cancellationToken = default)
    {
        var hash = HashOf(address);

        if (hash is null)
        {
            return;
        }

        var document = await LoadAsync(cancellationToken);
        var changed = optedOut ? document.AddressHashes.Add(hash) : document.AddressHashes.Remove(hash);

        if (changed)
        {
            await _session.SaveAsync(document, cancellationToken: cancellationToken);
        }
    }

    private async Task<SurveyInvitationOptOutDocument> LoadAsync(CancellationToken cancellationToken)
        => _document ??= await _session.Query<SurveyInvitationOptOutDocument>().FirstOrDefaultAsync(cancellationToken) ?? new SurveyInvitationOptOutDocument();
}
