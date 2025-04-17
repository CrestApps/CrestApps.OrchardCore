
namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

public sealed class StdioMcpConnectionMetadata
{
    public string Command { get; set; }

    public string[] Arguments { get; set; }

    public string WorkingDirectory { get; set; }

    public Dictionary<string, string> EnvironmentVariables { get; set; }
}
