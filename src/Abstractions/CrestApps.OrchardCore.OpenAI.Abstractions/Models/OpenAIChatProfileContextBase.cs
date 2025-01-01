namespace CrestApps.OrchardCore.OpenAI.Models;

public abstract class OpenAIChatProfileContextBase
{
    public OpenAIChatProfile Profile { get; }

    public OpenAIChatProfileContextBase(OpenAIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = profile;
    }
}
