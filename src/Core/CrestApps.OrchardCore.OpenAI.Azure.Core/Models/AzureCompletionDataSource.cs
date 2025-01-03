using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class AzureCompletionDataSource
{
    public string Type { get; set; }

    public JsonObject Parameters { get; set; }
}
