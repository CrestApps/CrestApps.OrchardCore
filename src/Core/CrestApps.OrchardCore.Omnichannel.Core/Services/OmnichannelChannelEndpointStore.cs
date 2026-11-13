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

        // The address answers on a channel when it has that capability. Capabilities are channel names matched
        // case-insensitively, so a record written when the constant was "Sms" still routes its inbound traffic. The
        // value is canonicalized to E.164 by the manager before this call, so it is matched exactly.
        //
        // An email address is compared without regard to case, so an address saved before addresses were stored in
        // lower case still receives its mail.
        var comparison = string.Equals(channel, OmnichannelConstants.Channels.Email, StringComparison.OrdinalIgnoreCase)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return document.Records.Values.FirstOrDefault(x => string.Equals(x.Value, serviceAddress, comparison) && x.HasCapability(channel));
    }

    /// <inheritdoc/>
    public override async ValueTask<OmnichannelChannelEndpoint> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var endpoint = await base.FindByIdAsync(id, cancellationToken);

        if (endpoint is not null)
        {
            return endpoint;
        }

        // A number listed once per channel was merged into one address, and activities and history still name the
        // record that was merged away. They find the address it became.
        var document = await DocumentManager.GetOrCreateImmutableAsync();
        var merged = document.Records.Values.FirstOrDefault(record => record.IsKnownAs(id));

        return merged is null ? null : await base.FindByIdAsync(merged.ItemId, cancellationToken);
    }
}
