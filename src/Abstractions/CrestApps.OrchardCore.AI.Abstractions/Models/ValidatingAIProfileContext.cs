namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatingAIProfileContext : AIProfileContextBase
{
    public ValidationResultDetails Result { get; } = new();

    public ValidatingAIProfileContext(AIProfile profile)
        : base(profile)
    {
    }
}
