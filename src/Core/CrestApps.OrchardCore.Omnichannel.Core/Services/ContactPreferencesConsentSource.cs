using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The CRM's consent source: a contact's do-not-call, do-not-text and do-not-e-mail flags, followed to every record that
/// holds the same number (<see cref="IContactOptOutResolver"/>), and the opt-outs recorded against an address no contact
/// held. A change for a contact sets or clears the contact's flag; an opt-out for an address no contact holds is
/// recorded against the address, and an opt-in lifts it.
/// </summary>
public sealed class ContactPreferencesConsentSource : IConsentSource
{
    private readonly IContactOptOutResolver _optOutResolver;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactPreferencesConsentSource"/> class.
    /// </summary>
    /// <param name="optOutResolver">The resolver that reads the contacts' flags and the address opt-outs.</param>
    /// <param name="contentManager">The content manager that loads and updates a contact.</param>
    /// <param name="clock">The clock.</param>
    public ContactPreferencesConsentSource(IContactOptOutResolver optOutResolver, IContentManager contentManager, IClock clock)
    {
        _optOutResolver = optOutResolver;
        _contentManager = contentManager;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Name
        => ConsentSourceNames.ContactPreferences;

    /// <inheritdoc />
    public async Task<bool> HasOptedOutAsync(ConsentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!IsContactKind(query.ContactKind))
        {
            return false;
        }

        var contact = query.Contact ?? (string.IsNullOrEmpty(query.ContactId) ? null : await _contentManager.GetAsync(query.ContactId, VersionOptions.Latest));

        if (contact is not null && await _optOutResolver.HasOptedOutAsync(contact, query.Channel, cancellationToken))
        {
            return true;
        }

        return await _optOutResolver.HasAddressOptedOutAsync(query.Channel, query.Address, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RecordAsync(string channel, string address, ConsentChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (!IsContactKind(change.ContactKind) || !IsKeptChannel(channel))
        {
            return;
        }

        var contact = string.IsNullOrEmpty(change.ContactId) ? null : await _contentManager.GetAsync(change.ContactId, VersionOptions.Latest);

        if (change.OptedOut && contact is null)
        {
            // Nobody on file holds the address, but the person is owed silence on it all the same, so the opt-out is
            // recorded against the address. Every send path asks about it.
            await _optOutResolver.RecordAddressOptOutAsync(
                new ContactAddressOptOut
                {
                    Channel = channel,
                    Address = address,
                    Source = change.Source,
                    OptedOutUtc = _clock.UtcNow,
                },
                cancellationToken);

            return;
        }

        if (!change.OptedOut)
        {
            // An opt-out recorded while no contact held the address is lifted too, whoever holds it now.
            await _optOutResolver.ClearAddressOptOutAsync(channel, address, cancellationToken);
        }

        if (contact is null)
        {
            return;
        }

        var now = _clock.UtcNow;

        contact.Alter<OmnichannelContactPart>(part =>
        {
            switch (channel)
            {
                case OmnichannelConstants.Channels.Phone:
                    part.SetDoNotCall(change.OptedOut, now);
                    break;
                case OmnichannelConstants.Channels.Sms:
                    part.SetDoNotSms(change.OptedOut, now);
                    break;
                case OmnichannelConstants.Channels.Email:
                    part.SetDoNotEmail(change.OptedOut, now);
                    break;
            }
        });

        await _contentManager.UpdateAsync(contact);
    }

    private static bool IsContactKind(string kind)
        => string.IsNullOrEmpty(kind) || string.Equals(kind, ConsentContactKinds.Contact, StringComparison.Ordinal);

    private static bool IsKeptChannel(string channel)
        => channel is OmnichannelConstants.Channels.Phone or OmnichannelConstants.Channels.Sms or OmnichannelConstants.Channels.Email;
}
