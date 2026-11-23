using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Makes mailing lists honor an e-mail opt-out recorded anywhere: a "stop all marketing", a CRM do-not-email or an
/// unsubscribe from another module unsubscribes the address from every list, which also reaches each list's provider.
/// It only reacts: list membership is not consent, so it never says an address opted out, and an opt-in changes no list
/// (joining a list needs the person's own sign-up). It ignores the changes the mailing lists module records itself.
/// </summary>
public sealed class MailingListConsentSource : IConsentSource
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListConsentSource"/> class.
    /// </summary>
    /// <param name="services">
    /// The services, from which the member service is resolved when a change arrives. The member service asks the
    /// consent service, which asks every consent source, so taking it in the constructor would be a dependency cycle.
    /// </param>
    public MailingListConsentSource(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc/>
    public string Name
        => MailingListsConstants.ConsentSourceName;

    /// <inheritdoc/>
    public Task<bool> HasOptedOutAsync(ConsentQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <inheritdoc/>
    public async Task RecordAsync(string channel, string address, ConsentChange change, CancellationToken cancellationToken = default)
    {
        if (!ShouldUnsubscribe(channel, change) || string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        await _services.GetRequiredService<IMailingListMemberService>().UnsubscribeAsync(listId: null, address, MailingListMemberSources.Consent, change.Source, cancellationToken);
    }

    /// <summary>
    /// Returns whether a recorded consent change unsubscribes the address from every list: an e-mail opt-out for
    /// marketing or for every purpose, not recorded by the mailing lists module itself.
    /// </summary>
    /// <param name="channel">The channel of the change.</param>
    /// <param name="change">The change.</param>
    /// <returns><see langword="true"/> when every list is unsubscribed.</returns>
    public static bool ShouldUnsubscribe(string channel, ConsentChange change)
        => change is { OptedOut: true }
            && string.Equals(channel, ConsentChannels.Email, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(change.Source, MailingListsConstants.ConsentSourceName, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrEmpty(change.Purpose) || string.Equals(change.Purpose, ConsentPurposes.Marketing, StringComparison.OrdinalIgnoreCase));
}
