using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI;

public interface IAIDeploymentProvider
{
    /// <summary>
    /// Gets the unique technical name of the provider.
    /// <para>
    /// This name is used to identify the source of the deployment 
    /// It should be unique across different sources to avoid conflicts.
    /// </para>
    /// </summary>
    string TechnicalName { get; }

    /// <summary>
    /// Gets a localized display name for the deployment.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets a localized description for the source.
    /// <para>
    /// This description provides more information about the source and its purpose.
    /// It is intended for display in user interfaces where users can select or configure 
    /// AI Deployment.
    /// </para>
    /// </summary>
    LocalizedString Description { get; }
}
