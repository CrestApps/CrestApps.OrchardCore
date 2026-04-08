namespace CrestApps.Core.Mvc.Web.Areas.AI.Models;

/// <summary>
/// Options for configuring CrestApps AI services via appsettings.json.
/// </summary>
public sealed class CrestAppsAIOptions
{
    /// <summary>
    /// The default AI provider name (e.g., "OpenAI", "AzureOpenAI").
    /// </summary>
    public string DefaultProvider { get; set; }

    /// <summary>
    /// The default deployment/model name to use.
    /// </summary>
    public string DefaultDeployment { get; set; }
}
