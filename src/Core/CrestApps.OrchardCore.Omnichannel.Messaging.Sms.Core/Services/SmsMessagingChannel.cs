using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Flows.Models;
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
        SupportsMedia = false,
        SupportsDeliveryReceipts = true,
        SupportsBroadcast = true,
        ObservesQuietHours = true,
    };

    private readonly ISmsDispatcher _dispatcher;
    private readonly ISession _session;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsMessagingChannel"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher that routes a send to the provider owning the number.</param>
    /// <param name="session">The session the contact indexes are read from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SmsMessagingChannel(
        ISmsDispatcher dispatcher,
        ISession session,
        IStringLocalizer<SmsMessagingChannel> stringLocalizer)
    {
        _dispatcher = dispatcher;
        _session = session;
        S = stringLocalizer;
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
    public Task<MessageDispatchResult> SendAsync(MessagingOutboundMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        return _dispatcher.SendAsync(
            new SmsMessage
            {
                From = message.ServiceAddress,
                To = message.ContactAddress,
                Body = message.Body,
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public bool IsOptedOut(ContentItem contact)
        => contact is not null &&
            contact.TryGet<OmnichannelContactPart>(out var part) &&
            part.DoNotSms;

    /// <inheritdoc/>
    public IReadOnlyList<string> GetContactAddresses(ContentItem contact)
    {
        if (contact is null ||
            !contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bag) ||
            bag.ContentItems is null)
        {
            return [];
        }

        var cells = new List<string>();
        var others = new List<string>();

        foreach (var method in bag.ContentItems)
        {
            if (!string.Equals(method.ContentType, OmnichannelConstants.ContentTypes.PhoneNumber, StringComparison.Ordinal) ||
                !method.TryGet<PhoneNumberInfoPart>(out var phonePart))
            {
                continue;
            }

            var number = NormalizeAddress(phonePart.Number?.PhoneNumber);

            if (string.IsNullOrEmpty(number))
            {
                continue;
            }

            // A cell number is the one that can actually receive a text, so it leads whatever order the contact's
            // methods were entered in.
            if (string.Equals(phonePart.Type?.Text, "Cell", StringComparison.OrdinalIgnoreCase))
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

        return matches
            .Select(match => match.ContentItemId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
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
