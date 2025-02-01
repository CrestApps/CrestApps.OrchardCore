namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatingAIChatProfileContext : AIChatProfileContextBase
{
    public AIChatProfileValidateResult Result { get; } = new();

    public ValidatingAIChatProfileContext(AIChatProfile profile)
        : base(profile)
    {
    }
}
