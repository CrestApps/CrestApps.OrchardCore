namespace CrestApps.OrchardCore.OpenAI.Functions;

public class OpenAIChatFunctionParameters
{
    public OpenAIChatFunctionPropertyType Type { get; set; } = OpenAIChatFunctionPropertyType.Object;

    public Dictionary<string, IOpenAIChatFunctionProperty> Properties { get; } = [];

    public IEnumerable<string> Required
        => Properties.Where(x => x.Value.IsRequired)
        .Select(x => x.Key);
}
