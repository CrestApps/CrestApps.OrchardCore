namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatedAIProfileContext : AIProfileContextBase
{
    public readonly AIValidateResult Result;

    public ValidatedAIProfileContext(AIProfile profile, AIValidateResult result)
        : base(profile)
    {
        Result = result ?? new();
    }
}
