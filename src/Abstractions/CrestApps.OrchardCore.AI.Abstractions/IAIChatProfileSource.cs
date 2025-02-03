using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI;

public interface IAIChatProfileSource
{
    /// <summary>
    /// Gets the unique technical name of the profile source.
    /// <para>
    /// This name is used to identify the source of the profile 
    /// It should be unique across different sources to avoid conflicts.
    /// </para>
    /// </summary>
    string TechnicalName { get; }

    /// <summary>
    /// Get the unique technical name for the provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets a localized display name for the profile.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets a localized description for the source.
    /// <para>
    /// This description provides more information about the source and its purpose.
    /// It is intended for display in user interfaces where users can select or configure 
    /// AI Chat profiles.
    /// </para>
    /// </summary>
    LocalizedString Description { get; }
}
