namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatingAIProfileContext : AIProfileContextBase
{
    public AIProfileValidateResult Result { get; } = new();

    public ValidatingAIProfileContext(AIProfile profile)
        : base(profile)
    {
    }
}
