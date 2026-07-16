using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Default <see cref="IInboundContactLookup"/> implementation that matches contacts by their
/// normalized (E.164) primary cell and home phone numbers using the Omnichannel contact index.
/// </summary>
public sealed class InboundContactLookup : IInboundContactLookup
{
    private readonly ISession _session;
    private readonly IPhoneNumberService _phoneNumberService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundContactLookup"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to query the contact index.</param>
    /// <param name="phoneNumberService">The phone number service used to normalize numbers to E.164.</param>
    public InboundContactLookup(
        ISession session,
        IPhoneNumberService phoneNumberService)
    {
        _session = session;
        _phoneNumberService = phoneNumberService;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> FindContactItemIdsAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return [];
        }

        var normalized = Normalize(phoneNumber);

        if (string.IsNullOrEmpty(normalized))
        {
            return [];
        }

        var numbers = new[] { normalized };

        var cellMatches = await _session
            .QueryIndex<OmnichannelContactIndex>(index =>
                index.Published && index.NormalizedPrimaryCellPhoneNumber.IsIn(numbers))
            .ListAsync(cancellationToken);

        var homeMatches = await _session
            .QueryIndex<OmnichannelContactIndex>(index =>
                index.Published && index.NormalizedPrimaryHomePhoneNumber.IsIn(numbers))
            .ListAsync(cancellationToken);

        var ids = new List<string>();

        foreach (var index in cellMatches.Concat(homeMatches))
        {
            if (!string.IsNullOrEmpty(index.ContentItemId) && !ids.Contains(index.ContentItemId))
            {
                ids.Add(index.ContentItemId);
            }
        }

        return ids;
    }

    private string Normalize(string phoneNumber)
    {
        if (_phoneNumberService.TryFormatToE164(phoneNumber, null, out var e164) && !string.IsNullOrEmpty(e164))
        {
            return e164;
        }

        return new string(phoneNumber.Where(char.IsDigit).ToArray());
    }
}
