namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatingAIToolInstanceContext : AIToolInstanceContextBase
{
    public AIValidateResult Result { get; } = new();

    public ValidatingAIToolInstanceContext(AIToolInstance instance)
        : base(instance)
    {
    }
}
