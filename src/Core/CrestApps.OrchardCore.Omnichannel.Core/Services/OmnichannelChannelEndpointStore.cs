using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

public sealed class OmnichannelChannelEndpointStore : Catalog<OmnichannelChannelEndpoint>, IOmnichannelChannelEndpointStore
{
    public OmnichannelChannelEndpointStore(
        IDocumentManager<DictionaryDocument<OmnichannelChannelEndpoint>> documentManager)
        : base(documentManager)
    {
    }

    public async ValueTask<OmnichannelChannelEndpoint> GetByServiceAddressAsync(string channel, string serviceAddress)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentException.ThrowIfNullOrEmpty(serviceAddress);

        var document = await DocumentManager.GetOrCreateMutableAsync();

        return document.Records.Values.FirstOrDefault(x => x.Channel == channel && x.Value == serviceAddress);
    }
}
