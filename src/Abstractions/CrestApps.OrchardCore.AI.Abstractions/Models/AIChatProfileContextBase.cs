namespace CrestApps.OrchardCore.AI.Models;

public abstract class AIChatProfileContextBase
{
    public AIChatProfile Profile { get; }

    public AIChatProfileContextBase(AIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = profile;
    }
}
