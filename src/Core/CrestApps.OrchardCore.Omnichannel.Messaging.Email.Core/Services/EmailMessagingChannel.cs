using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// The email channel of the messaging workspace. Addresses are email addresses in their canonical lower-case form, a
/// message leaves through the transport of the address it is sent from, and a contact is reached at the email addresses
/// on their record and opted out when the consent service says so (the Omnichannel contact's <c>Do not email</c> flag).
/// A contact is found by any of its addresses, through <see cref="OmnichannelContactEmailAddressIndex"/>.
/// </summary>
public sealed class EmailMessagingChannel : IMessagingChannel
{
    // A search reads a few more address rows than it returns records, because a record with two matching addresses
    // has a row for each.
    private const int SearchRowsPerRecord = 4;

    // The characters LIKE reads as wildcards on one database or another.
    private static readonly char[] _likeWildcards = ['%', '_', '[', ']'];

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
            Formats = FileFormats.All,
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
    private readonly IContactMethodsAccessor _contactMethods;
    private readonly IConsentService _consentService;
    private readonly ISession _session;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailMessagingChannel"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher that composes and sends each email, resolved on first send.</param>
    /// <param name="contactTypeProvider">The provider of the content types that are leads.</param>
    /// <param name="session">The session the contact indexes are read from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="contactMethods">Reads a contact's email addresses.</param>
    /// <param name="consentService">Answers whether a contact or an address has opted out of email.</param>
    public EmailMessagingChannel(
        Lazy<IEmailDispatcher> dispatcher,
        IOmnichannelContactTypeProvider contactTypeProvider,
        ISession session,
        IStringLocalizer<EmailMessagingChannel> stringLocalizer,
        IContactMethodsAccessor contactMethods,
        IConsentService consentService)
    {
        _dispatcher = dispatcher;
        _contactTypeProvider = contactTypeProvider;
        _session = session;
        S = stringLocalizer;
        _contactMethods = contactMethods;
        _consentService = consentService;
    }

    /// <inheritdoc/>
    public string Name => EmailChannelConstants.ChannelName;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Email"];

    /// <inheritdoc/>
    public string IconCssClass => "fa-solid fa-envelope";

    /// <inheritdoc/>
    /// <remarks>Blue, so an email tab is never mistaken for the green SMS one.</remarks>
    public string AccentColor => "var(--bs-primary)";

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
    public async Task<bool> IsOptedOutAsync(ContentItem contact, string address, CancellationToken cancellationToken = default)
    {
        // Asked of the consent service, as the SMS channel does, so an unsubscribe recorded on another record that holds
        // the address counts as well as this contact's own flag.
        var consent = await _consentService.GetAsync(new ConsentQuery
        {
            Channel = ConsentChannels.Email,
            Address = address,
            ContactKind = ConsentContactKinds.Contact,
            ContactId = contact?.ContentItemId,
            Contact = contact,
        }, cancellationToken);

        return consent.HasOptedOut;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetContactAddresses(ContentItem contact)
    {
        if (contact is null)
        {
            return [];
        }

        return _contactMethods.Get(contact).EmailAddresses
            .Select(emailAddress => emailAddress?.Email)
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

        // Every address of a record has a row, so mail from (or an unsubscribe of) a contact's second address finds
        // the contact as surely as its first.
        IEnumerable<IOmnichannelContactMatch> matches = await _session.QueryIndex<OmnichannelContactEmailAddressIndex>(
                index => index.NormalizedEmailAddress == normalized && index.Published)
            .ListAsync(cancellationToken);

        // Records saved before the per-address index existed get their rows from a background backfill, which also fills
        // their canonical primary email. Until it reaches them, that column is empty too, so they are found by the primary
        // email as it was saved: in the spelling given and in its canonical form, because PostgreSQL and SQLite compare
        // text by case.
        // Each lookup is a seek on an index of its own column; one query joining them with OR scanned the whole table.
        if (!matches.Any())
        {
            matches = await _session.QueryIndex<OmnichannelContactIndex>(
                    index => index.NormalizedPrimaryEmailAddress == normalized && index.Published)
                .ListAsync(cancellationToken);
        }

        if (!matches.Any())
        {
            var spellings = OmnichannelEmailAddress.WithCanonicalForms([address]);

            matches = await _session.QueryIndex<OmnichannelContactIndex>(
                    index => index.PrimaryEmailAddress.IsIn(spellings) && index.Published)
                .ListAsync(cancellationToken);
        }

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

        // The search runs as LIKE '%term%', where '_' and '%' (both common in addresses) are wildcards and SQL Server
        // also reads '['. The database is asked for the longest stretch of the term without them, and the rows it returns
        // are then held to the whole term as typed.
        var segment = GetLikeSafeSegment(normalized);

        if (segment.Length < 3)
        {
            return [];
        }

        var types = contactTypes.ToArray();
        var limit = Math.Max(1, take);
        var filtered = segment.Length != normalized.Length;

        // The address index carries the version and the content type, so the search reads it alone, across every
        // address of each record rather than only the first. A search that is filtered afterwards reads more rows, so
        // the rows the filter drops do not leave it short.
        var rows = await _session.QueryIndex<OmnichannelContactEmailAddressIndex>(
                index => index.Latest && index.ContentType.IsIn(types) && index.NormalizedEmailAddress.Contains(segment))
            .Take(limit * SearchRowsPerRecord * (filtered ? SearchRowsPerRecord : 1))
            .ListAsync(cancellationToken);

        return rows
            .Where(row => !filtered || (row.NormalizedEmailAddress?.Contains(normalized, StringComparison.Ordinal) ?? false))
            .Select(row => row.ContentItemId)
            .Where(contentItemId => !string.IsNullOrEmpty(contentItemId))
            .Distinct(StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    // The longest part of a search term that holds none of the characters LIKE treats as wildcards.
    internal static string GetLikeSafeSegment(string term)
        => string.IsNullOrEmpty(term)
            ? string.Empty
            : term.Split(_likeWildcards, StringSplitOptions.RemoveEmptyEntries)
                .OrderByDescending(part => part.Length)
                .FirstOrDefault() ?? string.Empty;
}
