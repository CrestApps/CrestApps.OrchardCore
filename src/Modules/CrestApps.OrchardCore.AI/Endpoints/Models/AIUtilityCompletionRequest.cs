namespace CrestApps.OrchardCore.AI.Endpoints.Models;

internal sealed class AIUtilityCompletionRequest
{
    public string ProfileId { get; set; }

    public string Prompt { get; set; }
}
