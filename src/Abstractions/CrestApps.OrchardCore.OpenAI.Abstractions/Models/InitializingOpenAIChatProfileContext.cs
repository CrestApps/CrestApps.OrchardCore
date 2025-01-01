using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class InitializingOpenAIChatProfileContext : OpenAIChatProfileContextBase
{
    public JsonNode Data { get; }

    public InitializingOpenAIChatProfileContext(OpenAIChatProfile profile, JsonNode data)
        : base(profile)
    {
        Data = data ?? new JsonObject();
    }
}
