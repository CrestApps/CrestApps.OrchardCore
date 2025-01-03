using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public abstract class OpenAIChatFunctionBase : IOpenAIChatFunction
{
    public abstract string Name { get; }

    public abstract string Description { get; }

    public OpenAIChatFunctionParameters Parameters { get; } = new();

    public abstract Task<string> InvokeAsync(JsonObject arguments);

    public void DefineProperty(string name, IOpenAIChatFunctionProperty property)
    {
        Parameters.Properties[name] = property;
    }
}
