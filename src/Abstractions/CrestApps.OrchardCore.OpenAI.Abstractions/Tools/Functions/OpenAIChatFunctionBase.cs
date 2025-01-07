using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public abstract class OpenAIChatFunctionBase : IOpenAIChatFunction
{
    public abstract string Name { get; }

    public abstract string Description { get; }

    public OpenAIChatFunctionType Parameters { get; } = new();

    public OpenAIChatFunctionType ReturnType { get; set; }

    public abstract Task<object> InvokeAsync(JsonObject arguments);

    public void DefineInputProperty(string name, IOpenAIChatFunctionProperty property)
    {
        Parameters.Properties[name] = property;
        ReturnType = new OpenAIChatFunctionType()
        {
            Type = OpenAIChatFunctionPropertyType.String,
        };
    }
}
