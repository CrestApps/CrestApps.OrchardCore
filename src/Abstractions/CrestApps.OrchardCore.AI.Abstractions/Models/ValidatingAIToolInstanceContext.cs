namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatingAIToolInstanceContext : AIToolInstanceContextBase
{
    public ValidationResultDetails Result { get; } = new();

    public ValidatingAIToolInstanceContext(AIToolInstance instance)
        : base(instance)
    {
    }
}
