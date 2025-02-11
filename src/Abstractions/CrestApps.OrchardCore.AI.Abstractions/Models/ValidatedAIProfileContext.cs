namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatedAIProfileContext : AIProfileContextBase
{
    public readonly AIProfileValidateResult Result;

    public ValidatedAIProfileContext(AIProfile profile, AIProfileValidateResult result)
        : base(profile)
    {
        Result = result ?? new();
    }
}
