namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatedAIToolInstanceContext : AIToolInstanceContextBase
{
    public AIValidateResult Result { get; } = new();

    public ValidatedAIToolInstanceContext(AIToolInstance instance, AIValidateResult result)
        : base(instance)
    {
        Result = result ?? new AIValidateResult();
    }
}
