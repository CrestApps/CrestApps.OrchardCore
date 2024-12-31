using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Functions;

public sealed class ObjectFunctionProperty : IOpenAIChatFunctionObjectProperty
{
    public OpenAIChatFunctionPropertyType Type => OpenAIChatFunctionPropertyType.Object;

    public string Description { get; set; }

    public object DefaultValue { get; set; }

    public Dictionary<string, IOpenAIChatFunctionProperty> Properties { get; } = [];

    [JsonIgnore]
    public bool IsRequired { get; set; }

    public IEnumerable<string> Required { get; set; }

    public ObjectFunctionProperty()
    {
        Required = Properties.Where(x => x.Value.IsRequired).Select(x => x.Key);
    }

}
