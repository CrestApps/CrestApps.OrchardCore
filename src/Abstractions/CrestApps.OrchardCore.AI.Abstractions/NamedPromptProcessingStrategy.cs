using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public abstract class NamedPromptProcessingStrategy : IPromptProcessingStrategy
{
    protected NamedPromptProcessingStrategy(string name)
    {
        Name = name;
    }

    protected string Name { get; }

    public Task ProcessAsync(IntentProcessingContext context, CancellationToken cancellationToken = default)
    {
        if (context.Result?.Intent is not null && !string.Equals(context.Result.Intent, Name, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }


        return ProceedAsync(context, cancellationToken);
    }

    protected abstract Task ProceedAsync(IntentProcessingContext context, CancellationToken cancellationToken);
}
