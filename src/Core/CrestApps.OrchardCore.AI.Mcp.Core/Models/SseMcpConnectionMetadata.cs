namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

public sealed class SseMcpConnectionMetadata
{
    public Uri Endpoint { get; set; }

    public Dictionary<string, string> AdditionalHeaders { get; set; }
}
