namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public class OpenAIChatFunctionType
{
    public OpenAIChatFunctionPropertyType Type { get; set; } = OpenAIChatFunctionPropertyType.Object;

    public Dictionary<string, IOpenAIChatFunctionProperty> Properties { get; } = [];

    public IEnumerable<string> Required
        => Properties.Where(x => x.Value.IsRequired)
        .Select(x => x.Key);
}
