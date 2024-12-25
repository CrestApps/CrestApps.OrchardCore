namespace CrestApps.OrchardCore.OpenAI.ViewModels.Sessions;

public sealed class ChatSessionMessageViewModel
{
    public string Id { get; set; }

    public string Role { get; set; }

    public string Prompt { get; set; }

    public string Title { get; set; }

    public bool FunctionalGenerated { get; set; }
}
