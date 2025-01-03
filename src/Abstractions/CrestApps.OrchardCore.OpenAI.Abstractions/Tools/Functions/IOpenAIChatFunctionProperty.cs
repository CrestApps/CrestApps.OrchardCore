namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public interface IOpenAIChatFunctionProperty
{
    OpenAIChatFunctionPropertyType Type { get; }

    string Description { get; }

    bool IsRequired { get; set; }

    object DefaultValue { get; set; }
}
