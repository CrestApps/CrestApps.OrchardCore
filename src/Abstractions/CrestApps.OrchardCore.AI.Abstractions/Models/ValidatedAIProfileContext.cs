namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatedAIProfileContext : AIProfileContextBase
{
    public readonly ValidationResultDetails Result;

    public ValidatedAIProfileContext(AIProfile profile, ValidationResultDetails result)
        : base(profile)
    {
        Result = result ?? new();
    }
}
