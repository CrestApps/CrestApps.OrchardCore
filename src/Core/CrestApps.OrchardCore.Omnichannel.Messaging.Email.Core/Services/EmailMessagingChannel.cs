using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Flows.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// The email channel of the messaging workspace. Addresses are email addresses in their canonical lower-case form, a
/// message leaves through the transport of the address it is sent from, and a contact is reached at the email addresses
/// on their record and opted out by the Omnichannel contact's <c>Do not email</c> flag.
/// </summary>
public sealed class EmailMessagingChannel : IMessagingChannel
{
    private static readonly MessagingChannelCapabilities _capabilities = new()
    {
        SupportsSubject = true,

        // Mail servers accept a message; whether it reaches the inbox is not reported, except by a bounce, which the
        // inbound receiver turns into a failed delivery on its own.
        SupportsDeliveryReceipts = false,
        SupportsBroadcast = true,

        // Email waits in the inbox until it is read, so it does not wake anyone at night.
        ObservesQuietHours = false,

        // An email carries pictures and documents alike, inside the message itself.
        Attachments = new MessagingAttachmentCapabilities
        {
            Formats = MessagingFileFormats.All,
            MaxCount = 10,
            MaxTotalBytes = 20 * 1024 * 1024,
            DeliveredAsLinks = false,
        },
    };

    // Lazy because the channel is built whenever the channel registry is, including while an address is being saved,
    // and the dispatcher itself reads the addresses: resolving it eagerly closes a dependency cycle the container cannot
    // report, and the scope deadlocks (the same reason the SMS channel resolves its dispatcher lazily).
    private readonly Lazy<IEmailDispatcher> _dispatcher;
    private readonly IOmnichannelContactTypeProvider _contactTypeProvider;
    private readonly ISession _session;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailMessagingChannel"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher that composes and sends each email, resolved on first send.</param>
    /// <param name="contactTypeProvider">The provider of the content types that are leads.</param>
    /// <param name="session">The session the contact indexes are read from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EmailMessagingChannel(
        Lazy<IEmailDispatcher> dispatcher,
        IOmnichannelContactTypeProvider contactTypeProvider,
        ISession session,
        IStringLocalizer<EmailMessagingChannel> stringLocalizer)
    {
        _dispatcher = dispatcher;
        _contactTypeProvider = contactTypeProvider;
        _session = session;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => EmailChannelConstants.ChannelName;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Email"];

    /// <inheritdoc/>
    public string IconCssClass => "fa-solid fa-envelope";

    /// <inheritdoc/>
    public int Order => 10;

    /// <inheritdoc/>
    public MessagingChannelCapabilities Capabilities => _capabilities;

    /// <inheritdoc/>
    public string NormalizeAddress(string address)
        => OmnichannelEmailAddress.Normalize(address);

    /// <inheritdoc/>
    public string FormatAddress(string address)
        => OmnichannelEmailAddress.Normalize(address) ?? address;

    /// <inheritdoc/>
    public bool IsValidAddress(string address)
        => OmnichannelEmailAddress.IsValid(address);

    /// <inheritdoc/>
    public Task<MessageDispatchResult> SendAsync(MessagingOutboundMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        return _dispatcher.Value.SendAsync(message, cancellationToken);
    }

    /// <inheritdoc/>
    public bool IsOptedOut(ContentItem contact)
        => contact is not null &&
            contact.TryGet<OmnichannelContactPart>(out var part) &&
            part.DoNotEmail;

    /// <inheritdoc/>
    public IReadOnlyList<string> GetContactAddresses(ContentItem contact)
    {
        if (contact is null ||
            !contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bag) ||
            bag.ContentItems is null)
        {
            return [];
        }

        return bag.ContentItems
            .Where(method => string.Equals(method.ContentType, OmnichannelConstants.ContentTypes.EmailAddress, StringComparison.Ordinal))
            .Select(method => method.TryGet<EmailInfoPart>(out var emailPart) ? emailPart.Email?.Text : null)
            .Where(OmnichannelEmailAddress.IsValid)
            .Select(OmnichannelEmailAddress.Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> FindContactIdsAsync(string address, CancellationToken cancellationToken = default)
    {
        var normalized = OmnichannelEmailAddress.Normalize(address);

        if (string.IsNullOrEmpty(normalized))
        {
            return [];
        }

        var matches = await _session.QueryIndex<OmnichannelContactIndex>(
                index => index.Published && index.NormalizedPrimaryEmailAddress == normalized)
            .ListAsync(cancellationToken);

        // Contacts come before leads and converted leads are left out, so a sender who is both a contact and a lead is
        // linked to the contact, and callers that take the first match always take the same one.
        var leadTypes = await _contactTypeProvider.GetLeadContentTypesAsync(cancellationToken);

        return OmnichannelContactMatches.Order(matches, leadTypes);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> SearchContactIdsByAddressAsync(
        string term,
        IReadOnlyCollection<string> contactTypes,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalized = term?.Trim().ToLowerInvariant();

        // Fewer than three characters matches half the tenant, which is not a search.
        if (string.IsNullOrEmpty(normalized) || normalized.Length < 3 || contactTypes is null || contactTypes.Count == 0)
        {
            return [];
        }

        var types = contactTypes.ToArray();

        var items = await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(types))
            .With<OmnichannelContactIndex>(index => index.NormalizedPrimaryEmailAddress.Contains(normalized))
            .Take(Math.Max(1, take))
            .ListAsync(cancellationToken);

        return items
            .Select(item => item.ContentItemId)
            .ToArray();
    }
}
