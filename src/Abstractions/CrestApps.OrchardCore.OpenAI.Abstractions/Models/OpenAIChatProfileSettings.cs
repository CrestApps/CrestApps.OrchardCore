namespace CrestApps.OrchardCore.OpenAI.Models;

public class OpenAIChatProfileSettings
{
    public bool LockSystemMessage { get; set; }

    public bool IsListable { get; set; } = true;

    public bool IsRemovable { get; set; } = true;
}
