using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CrestApps.AI.Mcp.Services;

public interface IMcpServerResourceService
{
    Task<IList<Resource>> ListAsync();

    Task<IList<ResourceTemplate>> ListTemplatesAsync();

    Task<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken = default);
}
