using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.TenantFeatureProfiles;

/// <summary>
/// What the tenant feature profile tools share.
/// </summary>
internal static class TenantFeatureProfileToolSupport
{
    /// <summary>
    /// The answer when a tool is called outside the Default tenant.
    /// </summary>
    public const string DefaultTenantOnlyMessage = "Tenant feature profiles are managed from the Default tenant only.";

    /// <summary>
    /// The name of the recipe step that creates or replaces feature profiles.
    /// </summary>
    public const string RecipeStepName = "FeatureProfiles";

    // The tenant setting that holds the IDs of the tenant's feature profiles, separated by commas.
    private const string FeatureProfileSetting = "FeatureProfile";

    /// <summary>
    /// Gets the IDs of the feature profiles a tenant uses.
    /// </summary>
    /// <param name="settings">The settings of the tenant.</param>
    /// <returns>The profile IDs.</returns>
    public static string[] GetProfileIds(ShellSettings settings)
        => (settings[FeatureProfileSetting] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
