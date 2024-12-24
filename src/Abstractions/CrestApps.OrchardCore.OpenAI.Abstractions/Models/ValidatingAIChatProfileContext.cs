namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class ValidatingAIChatProfileContext : AIChatProfileContextBase
{
    public AIChatProfileValidateResult Result { get; } = new();

    public ValidatingAIChatProfileContext(AIChatProfile profile)
        : base(profile)
    {
    }
}
