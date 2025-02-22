namespace CrestApps.OrchardCore.AI.Models;

public abstract class AIToolInstanceContextBase
{
    public AIToolInstance Instance { get; }

    public AIToolInstanceContextBase(AIToolInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        Instance = instance;
    }
}
