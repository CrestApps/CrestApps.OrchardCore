namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatingAIProfileContext : AIProfileContextBase
{
    public AIValidateResult Result { get; } = new();

    public ValidatingAIProfileContext(AIProfile profile)
        : base(profile)
    {
    }
}
