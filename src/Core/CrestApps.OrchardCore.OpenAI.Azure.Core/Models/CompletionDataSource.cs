using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class CompletionDataSource
{
    public string Type { get; set; }

    public JsonObject Parameters { get; set; }
}
