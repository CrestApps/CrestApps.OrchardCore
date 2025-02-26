using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.OpenAI.Core.Models;

public sealed class OpenAIConnectionDocument : Document
{
    public Dictionary<string, OpenAIConnection> Connections { get; init; } = [];

    public string DefaultConnectionId { get; set; }
}
