using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public interface IOpenAIChatFunction
{
    string Name { get; }

    string Description { get; }

    OpenAIChatFunctionType Parameters { get; }

    Task<object> InvokeAsync(JsonObject arguments);

    OpenAIChatFunctionType ReturnType { get; }
}
