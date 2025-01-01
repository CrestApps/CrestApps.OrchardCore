namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class LoadedOpenAIChatProfileContext : OpenAIChatProfileContextBase
{
    public LoadedOpenAIChatProfileContext(OpenAIChatProfile profile)
        : base(profile)
    {
    }
}
