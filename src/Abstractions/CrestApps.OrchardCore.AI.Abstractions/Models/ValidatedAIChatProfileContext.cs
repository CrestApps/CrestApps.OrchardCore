namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatedAIChatProfileContext : AIChatProfileContextBase
{
    public readonly AIChatProfileValidateResult Result;

    public ValidatedAIChatProfileContext(AIChatProfile profile, AIChatProfileValidateResult result)
        : base(profile)
    {
        Result = result ?? new();
    }
}
