namespace CrestApps.OrchardCore.Omnichannel.Messaging.Deployments;

/// <summary>
/// Names the recipe steps that carry messaging workspace configuration between environments.
/// </summary>
public static class MessagingDeploymentSteps
{
    /// <summary>
    /// The recipe step that carries the canned-response message templates.
    /// </summary>
    public const string MessageTemplate = "OmnichannelMessageTemplate";
}
