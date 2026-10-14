using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.Extensions.Configuration;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Decides which features a parent or child tenant may enable. It hides the features that would reveal or reach
/// sibling tenants, the Parent feature everywhere except in a parent, and the Child feature everywhere: a child gets
/// it through its configuration, so it can never be switched off from the features screen.
/// </summary>
/// <remarks>
/// A validation provider only filters what can be enabled. It does not switch off a feature that is already on.
/// </remarks>
public sealed class TenantHierarchyFeatureValidationProvider : IFeatureValidationProvider
{
    /// <summary>
    /// The features blocked in parent and child tenants.
    /// </summary>
    internal static readonly string[] BlockedInHierarchy =
    [
        "OrchardCore.OpenId.Validation",
    ];

    /// <summary>
    /// The features blocked in child tenants.
    /// </summary>
    internal static readonly string[] BlockedInChildren =
    [
        "OrchardCore.Deployment.Remote",
    ];

    /// <summary>
    /// The feature blocked in child tenants unless every child has its own database.
    /// </summary>
    internal const string SqlQueriesFeatureId = "OrchardCore.Queries.Sql";

    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantHierarchyFeatureValidationProvider"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    public TenantHierarchyFeatureValidationProvider(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    /// <inheritdoc/>
    public ValueTask<bool> IsFeatureValidAsync(string id)
        => ValueTask.FromResult(IsFeatureValid(_shellSettings, id));

    /// <summary>
    /// Returns whether a tenant may enable a feature.
    /// </summary>
    /// <param name="settings">The settings of the tenant.</param>
    /// <param name="featureId">The feature identifier.</param>
    internal static bool IsFeatureValid(ShellSettings settings, string featureId)
    {
        if (string.IsNullOrEmpty(featureId))
        {
            return false;
        }

        if (featureId == TenantHierarchyConstants.Features.Child)
        {
            return false;
        }

        if (featureId == TenantHierarchyConstants.Features.Parent)
        {
            return settings.IsParentTenant();
        }

        var role = settings.GetHierarchyRole();

        if (role is null)
        {
            return true;
        }

        if (featureId == TenantHierarchyConstants.Features.Platform)
        {
            return false;
        }

        if (BlockedInHierarchy.Contains(featureId, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (role != TenantHierarchyConstants.Roles.Child)
        {
            return true;
        }

        if (BlockedInChildren.Contains(featureId, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(featureId, SqlQueriesFeatureId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                settings[TenantHierarchyConstants.SettingsKeys.DatabaseStrategy],
                nameof(ChildDatabaseStrategy.DatabasePerChild),
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                settings[TenantHierarchyConstants.SettingsKeys.DatabaseStrategy],
                nameof(ChildDatabaseStrategy.SqlitePerChild),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !GetChildBlockedFeatures(settings).Contains(featureId, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetChildBlockedFeatures(ShellSettings settings)
    {
        return settings.ShellConfiguration
            .GetSection(TenantHierarchyConstants.SettingsKeys.BlockedFeatures)
            .GetChildren()
            .Select(section => section.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }
}
