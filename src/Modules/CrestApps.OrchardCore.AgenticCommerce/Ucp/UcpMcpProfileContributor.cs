namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Offers UCP's shopping service over the Model Context Protocol, at <c>ucp/mcp</c>.
/// </summary>
public sealed class UcpMcpProfileContributor : IUcpProfileContributor
{
    /// <inheritdoc />
    public Task ContributeAsync(UcpProfileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Services.Add(new UcpService
        {
            Transport = "mcp",
            Schema = UcpConstants.Url(context.Version, "services/shopping/mcp.openrpc.json"),
            Endpoint = $"{context.BaseUrl}/{UcpMcpEndpoints.Route}",
        });

        return Task.CompletedTask;
    }
}
