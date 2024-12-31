using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Functions;

public interface IOpenAIChatFunction
{
    string Name { get; }

    string Description { get; }

    OpenAIChatFunctionParameters Parameters { get; }

    Task<string> InvokeAsync(JsonObject arguments);
}
