using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;

/// <summary>
/// Stands in for the contact index: the contact's own preference decides, unless a test says somebody else reachable
/// at the same number has opted out. Opt-outs recorded against an address are kept in memory.
/// </summary>
internal sealed class SharedNumberOptOutResolver : IContactOptOutResolver
{
    /// <summary>
    /// Gets or sets whether another record sharing the contact's number has opted out.
    /// </summary>
    public bool SomebodyAtTheNumberOptedOut { get; set; }

    /// <summary>
    /// Gets the opt-outs recorded against an address.
    /// </summary>
    public List<ContactAddressOptOut> AddressOptOuts { get; } = [];

    public Task<bool> HasOptedOutAsync(ContentItem contact, string channel, CancellationToken cancellationToken = default)
        => Task.FromResult(
            contact is not null &&
            (SomebodyAtTheNumberOptedOut || OmnichannelContactPreferences.HasOptedOut(contact, channel)));

    public Task<bool> HasAddressOptedOutAsync(string channel, string address, CancellationToken cancellationToken = default)
        => Task.FromResult(AddressOptOuts.Any(optOut => Matches(optOut, channel, address)));

    public Task RecordAddressOptOutAsync(ContactAddressOptOut optOut, CancellationToken cancellationToken = default)
    {
        if (!AddressOptOuts.Any(existing => Matches(existing, optOut.Channel, optOut.Address)))
        {
            AddressOptOuts.Add(optOut);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ClearAddressOptOutAsync(string channel, string address, CancellationToken cancellationToken = default)
        => Task.FromResult(AddressOptOuts.RemoveAll(optOut => Matches(optOut, channel, address)) > 0);

    private static bool Matches(ContactAddressOptOut optOut, string channel, string address)
        => string.Equals(optOut.Channel, channel, StringComparison.Ordinal) &&
            string.Equals(optOut.Address?.Trim(), address?.Trim(), StringComparison.Ordinal);
}
