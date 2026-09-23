using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;

/// <summary>
/// Stands in for the contact index: the contact's own preference decides, unless a test says somebody else reachable
/// at the same number has opted out.
/// </summary>
internal sealed class SharedNumberOptOutResolver : IContactOptOutResolver
{
    /// <summary>
    /// Gets or sets whether another record sharing the contact's number has opted out.
    /// </summary>
    public bool SomebodyAtTheNumberOptedOut { get; set; }

    public Task<bool> HasOptedOutAsync(ContentItem contact, string channel, CancellationToken cancellationToken = default)
        => Task.FromResult(
            contact is not null &&
            (SomebodyAtTheNumberOptedOut || OmnichannelContactPreferences.HasOptedOut(contact, channel)));
}
