namespace CrestApps.OrchardCore.AI.Models;

public abstract class AIProfileContextBase
{
    public AIProfile Profile { get; }

    public AIProfileContextBase(AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = profile;
    }
}
