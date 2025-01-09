namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class DeletingOpenAIChatProfileContext : OpenAIChatProfileContextBase
{
    public DeletingOpenAIChatProfileContext(OpenAIChatProfile profile)
        : base(profile)
    {
    }
}
