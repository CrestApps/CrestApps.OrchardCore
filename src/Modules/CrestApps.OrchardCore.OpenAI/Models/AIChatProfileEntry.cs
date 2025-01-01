using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.OpenAI.Models;

public class AIChatProfileEntry
{
    public OpenAIChatProfile Profile { get; set; }

    public IShape Shape { get; set; }
}
