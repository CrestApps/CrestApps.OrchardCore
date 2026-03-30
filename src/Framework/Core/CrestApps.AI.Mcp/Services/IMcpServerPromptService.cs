using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CrestApps.AI.Mcp.Services;

public interface IMcpServerPromptService
{
    Task<IList<Prompt>> ListAsync();

    Task<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken = default);
}
