namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class SavedOpenAIChatProfileContext : OpenAIChatProfileContextBase
{
    public SavedOpenAIChatProfileContext(OpenAIChatProfile profile)
        : base(profile)
    {
    }
}
