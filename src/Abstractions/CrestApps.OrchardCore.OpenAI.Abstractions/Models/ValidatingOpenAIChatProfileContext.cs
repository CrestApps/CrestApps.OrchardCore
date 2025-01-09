namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class ValidatingOpenAIChatProfileContext : OpenAIChatProfileContextBase
{
    public OpenAIChatProfileValidateResult Result { get; } = new();

    public ValidatingOpenAIChatProfileContext(OpenAIChatProfile profile)
        : base(profile)
    {
    }
}
