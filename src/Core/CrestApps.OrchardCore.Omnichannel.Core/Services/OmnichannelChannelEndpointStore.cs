using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Provides a document-based implementation of <see cref="IOmnichannelChannelEndpointStore"/> for persisting and querying omnichannel channel endpoints.
/// </summary>
public sealed class OmnichannelChannelEndpointStore : Catalog<OmnichannelChannelEndpoint>, IOmnichannelChannelEndpointStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointStore"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager for channel endpoint records.</param>
    public OmnichannelChannelEndpointStore(
        IDocumentManager<DictionaryDocument<OmnichannelChannelEndpoint>> documentManager)
        : base(documentManager)
    {
    }

    /// <inheritdoc/>
    public async ValueTask<OmnichannelChannelEndpoint> GetByServiceAddressAsync(string channel, string serviceAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentException.ThrowIfNullOrEmpty(serviceAddress);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        // Channel is a well-known token ("Phone", "SMS"); match it case-insensitively so an endpoint persisted with
        // a different casing (for example legacy data written when the constant was "Sms") still routes its inbound
        // traffic. The value is canonicalized to E.164 by the manager before this call, so it is matched exactly.
        return document.Records.Values.FirstOrDefault(x =>
            string.Equals(x.Channel, channel, StringComparison.OrdinalIgnoreCase) && x.Value == serviceAddress);
    }
}
