using CrestApps.Core.AI.Copilot.Models;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Models;

public sealed class CopilotSettings
{
    public CopilotAuthenticationType AuthenticationType { get; set; }

    public string ClientId { get; set; }

    public string ProtectedClientSecret { get; set; }

    public string[] Scopes { get; set; } = ["user:email", "read:org"];

    public string ProviderType { get; set; }

    public string BaseUrl { get; set; }

    public string ProtectedApiKey { get; set; }

    public string WireApi { get; set; } = "completions";

    public string DefaultModel { get; set; }

    public string AzureApiVersion { get; set; }
}
