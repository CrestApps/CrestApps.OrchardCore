using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Sms;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;

/// <summary>
/// The SMS channel of the messaging workspace. Addresses are phone numbers, a message leaves through the
/// provider that owns the sending number, and a contact is reached on their cell (then home) number and opted out
/// by the Omnichannel contact's <c>Do not SMS</c> flag.
/// </summary>
public sealed class SmsMessagingChannel : IMessagingChannel
{
    private static readonly MessagingChannelCapabilities _capabilities = new()
    {
        SupportsSubject = false,
        SupportsDeliveryReceipts = true,
        SupportsBroadcast = true,
        ObservesQuietHours = true,
        AddressesArePhoneNumbers = true,

        // A text message carries pictures only (MMS), and the carriers refuse one much over a megabyte, so the
        // composer shrinks photos to fit.
        Attachments = new MessagingAttachmentCapabilities
        {
            Formats = MessagingFileFormats.Images,
            MaxCount = 10,
            MaxTotalBytes = 1024 * 1024,
            ShrinkImagesToFit = true,
        },
    };

    // Lazy because the channel is built whenever the channel registry is, including while a channel endpoint is being
    // saved (the endpoint address policy asks the registry), and the dispatcher itself reads endpoints: resolving it
    // eagerly closes a dependency cycle the container cannot report, and the scope deadlocks.
    private readonly Lazy<ISmsDispatcher> _dispatcher;
    private readonly IOmnichannelContactTypeProvider _contactTypeProvider;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IContactMethodsAccessor _contactMethods;
    private readonly ISession _session;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsMessagingChannel"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher that routes a send to the provider owning the number, resolved on first send.</param>
    /// <param name="session">The session the contact indexes are read from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="contactTypeProvider">The provider of the content types that are contacts.</param>
    /// <param name="phoneNumberService">The phone number service a typed number is validated with.</param>
    /// <param name="contactMethods">Reads a contact's phone numbers.</param>
    public SmsMessagingChannel(
        Lazy<ISmsDispatcher> dispatcher,
        ISession session,
        IStringLocalizer<SmsMessagingChannel> stringLocalizer,
        IOmnichannelContactTypeProvider contactTypeProvider,
        IPhoneNumberService phoneNumberService,
        IContactMethodsAccessor contactMethods)
    {
        _dispatcher = dispatcher;
        _session = session;
        S = stringLocalizer;
        _contactTypeProvider = contactTypeProvider;
        _phoneNumberService = phoneNumberService;
        _contactMethods = contactMethods;
    }

    /// <inheritdoc/>
    public string Name => OmnichannelConstants.Channels.Sms;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["SMS"];

    /// <inheritdoc/>
    public string IconCssClass => "fa-solid fa-comment-sms";

    /// <inheritdoc/>
    public int Order => 0;

    /// <inheritdoc/>
    public MessagingChannelCapabilities Capabilities => _capabilities;

    /// <inheritdoc/>
    public string NormalizeAddress(string address)
        => string.IsNullOrWhiteSpace(address) ? null : address.GetCleanedPhoneNumber();

    /// <inheritdoc/>
    public string FormatAddress(string address)
        => PhoneDisplayFormatter.Format(address);

    /// <inheritdoc/>
    public bool IsValidAddress(string address)
    {
        var normalized = NormalizeAddress(address);

        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        var digits = normalized.Count(char.IsDigit);

        // Anything a person could plausibly have typed as a number: digits with an optional leading '+', and
        // enough of them to be a real subscriber number. The provider is the authority on reachability.
        return digits >= 7 &&
            digits <= 15 &&
            normalized.Select((character, index) => char.IsDigit(character) || (index == 0 && character == '+')).All(valid => valid);
    }

    /// <inheritdoc/>
    public bool IsValidNewAddress(string address)
    {
        // A number typed for someone outside the contacts must be E.164 as written: a '+', the country code and a number
        // the numbering plan knows. A national number such as 7025550123 is not guessed into a country.
        if (string.IsNullOrEmpty(address) || address.Length < 2 || address[0] != '+' || address.AsSpan(1).ContainsAnyExceptInRange('0', '9'))
        {
            return false;
        }

        return _phoneNumberService.IsValidNumber(address, null);
    }

    /// <inheritdoc/>
    public Task<MessageDispatchResult> SendAsync(MessagingOutboundMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var sms = new SmsMessage
        {
            From = message.ServiceAddress,
            To = message.ContactAddress,
            Body = message.Body,
        };

        var mediaUrls = message.MediaUrls?.Where(url => !string.IsNullOrWhiteSpace(url)).ToArray() ?? [];

        return mediaUrls.Length == 0
            ? _dispatcher.Value.SendAsync(sms, cancellationToken)
            : _dispatcher.Value.SendAsync(sms, mediaUrls, cancellationToken);
    }

    /// <inheritdoc/>
    public bool IsOptedOut(ContentItem contact)
        => contact is not null &&
            contact.TryGet<OmnichannelContactPart>(out var part) &&
            part.DoNotSms;

    /// <inheritdoc/>
    public IReadOnlyList<string> GetContactAddresses(ContentItem contact)
    {
        if (contact is null)
        {
            return [];
        }

        var cells = new List<string>();
        var others = new List<string>();

        foreach (var phoneNumber in _contactMethods.Get(contact).PhoneNumbers)
        {
            var number = NormalizeAddress(phoneNumber.PhoneNumber);

            if (string.IsNullOrEmpty(number))
            {
                continue;
            }

            // A cell number is the one that can actually receive a text, so it leads whatever order the contact's
            // methods were entered in.
            if (ContactPhoneTypes.IsMobile(phoneNumber.Type))
            {
                cells.Add(number);
            }
            else
            {
                others.Add(number);
            }
        }

        return cells.Concat(others)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> FindContactIdsAsync(string address, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAddress(address);

        if (string.IsNullOrEmpty(normalized))
        {
            return [];
        }

        var matches = await _session.QueryIndex<OmnichannelContactIndex>(
                index => index.Published &&
                    (index.NormalizedPrimaryCellPhoneNumber == normalized || index.NormalizedPrimaryHomePhoneNumber == normalized))
            .ListAsync(cancellationToken);

        // Contacts come before leads and converted leads are left out, so a sender who is both a contact and a lead
        // is linked to the contact, and callers that take the first match always take the same one.
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
        var digits = new string((term ?? string.Empty).Where(char.IsDigit).ToArray());

        // Fewer than three digits matches half the tenant, which is not a search.
        if (digits.Length < 3 || contactTypes is null || contactTypes.Count == 0)
        {
            return [];
        }

        var types = contactTypes.ToArray();

        var items = await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(types))
            .With<OmnichannelContactIndex>(index =>
                index.NormalizedPrimaryCellPhoneNumber.Contains(digits) || index.PrimaryCellPhoneNumber.Contains(digits) ||
                index.NormalizedPrimaryHomePhoneNumber.Contains(digits) || index.PrimaryHomePhoneNumber.Contains(digits))
            .Take(Math.Max(1, take))
            .ListAsync(cancellationToken);

        return items
            .Select(item => item.ContentItemId)
            .ToArray();
    }
}
