using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// The do-not-call lists the site keeps itself, as a consent source: a phone number on a completed list has opted out
/// of calls. The lists are imported files, so a change isn't recorded here. The dialer screens the lists with the
/// national registries, under their own reason, and leaves this source out of its consent question.
/// </summary>
public sealed class DoNotCallListConsentSource : IConsentSource
{
    private readonly LocalDncRegistry _lists;

    /// <summary>
    /// Initializes a new instance of the <see cref="DoNotCallListConsentSource"/> class.
    /// </summary>
    /// <param name="registries">The registries, of which the site's own lists are read.</param>
    public DoNotCallListConsentSource(IEnumerable<INationalDoNotCallRegistry> registries)
    {
        _lists = registries.OfType<LocalDncRegistry>().FirstOrDefault();
    }

    /// <inheritdoc />
    public string Name
        => ConsentSourceNames.DoNotCallLists;

    /// <inheritdoc />
    public async Task<bool> HasOptedOutAsync(ConsentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_lists is null ||
            !string.Equals(query.Channel, ConsentChannels.Phone, StringComparison.Ordinal) ||
            !PhoneNumber.TryFromE164(query.Address?.Trim(), out var phoneNumber))
        {
            return false;
        }

        return (await _lists.GetRegisteredNumbersAsync([phoneNumber], cancellationToken)).Count > 0;
    }

    /// <inheritdoc />
    public Task RecordAsync(string channel, string address, ConsentChange change, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
