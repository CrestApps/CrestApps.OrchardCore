namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatedAIToolInstanceContext : AIToolInstanceContextBase
{
    public ValidationResultDetails Result { get; }

    public ValidatedAIToolInstanceContext(AIToolInstance instance, ValidationResultDetails result)
        : base(instance)
    {
        Result = result ?? new ValidationResultDetails();
    }
}
