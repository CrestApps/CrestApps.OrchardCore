using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Core.Models;

public sealed class AIChatSessionMessage : Entity
{
    public string Id { get; set; }

    public string Role { get; set; }

    public string Prompt { get; set; }

    public string Title { get; set; }

    public bool FunctionalGenerated { get; set; }
}
