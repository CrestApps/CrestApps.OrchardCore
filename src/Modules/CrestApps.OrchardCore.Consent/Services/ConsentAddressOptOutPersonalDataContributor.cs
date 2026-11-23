using System.Globalization;
using CrestApps.OrchardCore.Consent.Core.Models;
using CrestApps.OrchardCore.Consent.Core.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Consent.Services;

/// <summary>
/// Exports the opt-outs for one purpose (such as "stop all marketing") recorded against the person's e-mail addresses
/// and phone numbers, and keeps them when the person's data is erased.
/// </summary>
/// <remarks>
/// An opt-out is kept on erasure because it is what lets the site honor the person's objection: under GDPR Article 21(3)
/// personal data may no longer be processed for direct marketing once the person objects, and laws such as CAN-SPAM and
/// CASL require an unsubscribe to be honored. Erasing the record would let the address be sent marketing again, for
/// example after an import or a new sign-up form. The record holds only the address, the purpose, where it came from and
/// when, which is the least needed to honor the objection (GDPR Article 17(3)(b) and (e)).
/// </remarks>
internal sealed class ConsentAddressOptOutPersonalDataContributor : IPersonalDataContributor
{
    /// <summary>
    /// The record kind of an address opt-out in an export.
    /// </summary>
    public const string OptOutKind = "ConsentAddressOptOut";

    private static readonly string[] _phoneChannels = [ConsentChannels.Sms, ConsentChannels.Phone];

    private readonly IConsentAddressOptOutStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentAddressOptOutPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="store">The store of address opt-outs.</param>
    public ConsentAddressOptOutPersonalDataContributor(IConsentAddressOptOutStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var email in context.Subject.Emails)
        {
            Add(context, seen, await _store.ListAsync(ConsentChannels.Email, email, cancellationToken));
        }

        foreach (var phoneNumber in context.Subject.PhoneNumbers)
        {
            foreach (var channel in _phoneChannels)
            {
                Add(context, seen, await _store.ListAsync(channel, phoneNumber, cancellationToken));
            }
        }
    }

    /// <inheritdoc/>
    public Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The opt-outs are kept: see the remarks of the class.
        return Task.CompletedTask;
    }

    private static void Add(PersonalDataExportContext context, HashSet<string> seen, IReadOnlyList<ConsentAddressOptOut> records)
    {
        foreach (var record in records)
        {
            if (!seen.Add(record.ItemId))
            {
                continue;
            }

            var exported = context.Add(OptOutKind, record.ItemId, $"{record.Purpose} ({record.Channel})", record.OptedOutUtc);

            exported.Fields[nameof(ConsentAddressOptOut.Channel)] = record.Channel;
            exported.Fields[nameof(ConsentAddressOptOut.Address)] = record.Address;
            exported.Fields[nameof(ConsentAddressOptOut.Purpose)] = record.Purpose;
            exported.Fields[nameof(ConsentAddressOptOut.Source)] = record.Source;
            exported.Fields[nameof(ConsentAddressOptOut.OptedOutUtc)] = DateTime.SpecifyKind(record.OptedOutUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
        }
    }
}
