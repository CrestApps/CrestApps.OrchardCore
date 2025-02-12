namespace CrestApps.OrchardCore.AI.Endpoints.Models;

internal sealed class AICompletionRequest
{
    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public string Prompt { get; set; }

    public string SessionProfileId { get; set; }

    public bool IncludeHtmlResponse { get; set; } = true;
}
