using System.Globalization;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.Extensions.Configuration;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Writes the tenant hierarchy keys into shell settings. The changes are saved by
/// <see cref="IShellHost.UpdateShellSettingsAsync(ShellSettings)"/>.
/// </summary>
public static class TenantHierarchySettingsWriter
{
    /// <summary>
    /// Writes the policy of a parent tenant, replacing the previous one.
    /// </summary>
    /// <param name="settings">The shell settings of the parent tenant.</param>
    /// <param name="policy">The policy.</param>
    public static void WritePolicy(ShellSettings settings, ParentTenantPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(policy);

        const string prefix = TenantHierarchyConstants.SettingsKeys.Policy;

        ClearSection(settings, prefix);

        settings[$"{prefix}:{nameof(ParentTenantPolicy.MaxChildren)}"] = policy.MaxChildren.ToString(CultureInfo.InvariantCulture);
        settings[$"{prefix}:{nameof(ParentTenantPolicy.ChildHostPattern)}"] = NullIfEmpty(policy.ChildHostPattern);
        settings[$"{prefix}:{nameof(ParentTenantPolicy.DatabaseStrategy)}"] = policy.DatabaseStrategy.ToString();
        settings[$"{prefix}:{nameof(ParentTenantPolicy.DatabasePool)}"] = NullIfEmpty(policy.DatabasePool);
        settings[$"{prefix}:{nameof(ParentTenantPolicy.RequireMfa)}"] = policy.RequireMfa ? bool.TrueString : bool.FalseString;
        settings[$"{prefix}:{nameof(ParentTenantPolicy.SessionValidationInterval)}"] = policy.SessionValidationInterval.ToString("c", CultureInfo.InvariantCulture);
        settings[$"{prefix}:{nameof(ParentTenantPolicy.SessionIdleTimeout)}"] = policy.SessionIdleTimeout.ToString("c", CultureInfo.InvariantCulture);
        settings[$"{prefix}:{nameof(ParentTenantPolicy.SessionLifetime)}"] = policy.SessionLifetime.ToString("c", CultureInfo.InvariantCulture);
        settings[$"{prefix}:{nameof(ParentTenantPolicy.SwitcherMode)}"] = policy.SwitcherMode.ToString();
        settings[$"{prefix}:{nameof(ParentTenantPolicy.RemovalGraceDays)}"] = policy.RemovalGraceDays.ToString(CultureInfo.InvariantCulture);
        settings[$"{prefix}:{nameof(ParentTenantPolicy.Labels)}:{nameof(TenantHierarchyLabels.Parent)}"] = NullIfEmpty(policy.Labels?.Parent);
        settings[$"{prefix}:{nameof(ParentTenantPolicy.Labels)}:{nameof(TenantHierarchyLabels.Child)}"] = NullIfEmpty(policy.Labels?.Child);
        settings[$"{prefix}:{nameof(ParentTenantPolicy.Labels)}:{nameof(TenantHierarchyLabels.Children)}"] = NullIfEmpty(policy.Labels?.Children);

        WriteArray(settings, $"{prefix}:{nameof(ParentTenantPolicy.Recipes)}", policy.Recipes);
        WriteArray(settings, $"{prefix}:{nameof(ParentTenantPolicy.BlockedFeatures)}", policy.BlockedFeatures);
        WriteArray(settings, $"{prefix}:{nameof(ParentTenantPolicy.DeniedLocalPermissions)}", policy.DeniedLocalPermissions);
    }

    /// <summary>
    /// Copies the parts of a parent policy that a child tenant enforces itself into the child's shell settings.
    /// </summary>
    /// <param name="child">The shell settings of the child tenant.</param>
    /// <param name="policy">The policy of its parent.</param>
    public static void WriteChildPolicy(ShellSettings child, ParentTenantPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(policy);

        WriteArray(child, TenantHierarchyConstants.SettingsKeys.BlockedFeatures, policy.BlockedFeatures);
        WriteArray(child, TenantHierarchyConstants.SettingsKeys.DeniedLocalPermissions, policy.DeniedLocalPermissions);
    }

    /// <summary>
    /// Writes a list of values as an indexed configuration section, replacing the previous values.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    /// <param name="key">The section key.</param>
    /// <param name="values">The values.</param>
    public static void WriteArray(ShellSettings settings, string key, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrEmpty(key);

        ClearSection(settings, key);

        var index = 0;

        foreach (var value in values ?? [])
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            settings[$"{key}:{index.ToString(CultureInfo.InvariantCulture)}"] = value.Trim();
            index++;
        }
    }

    /// <summary>
    /// Adds a feature to the features the tenant configuration always enables, unless it is already there.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    /// <param name="featureId">The feature identifier.</param>
    public static void EnsureAlwaysEnabledFeature(ShellSettings settings, string featureId)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrEmpty(featureId);

        var features = GetAlwaysEnabledFeatures(settings);

        if (features.Contains(featureId, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        features.Add(featureId);
        WriteArray(settings, TenantHierarchyConstants.SettingsKeys.Features, features);
    }

    /// <summary>
    /// Removes a feature from the features the tenant configuration always enables.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    /// <param name="featureId">The feature identifier.</param>
    public static void RemoveAlwaysEnabledFeature(ShellSettings settings, string featureId)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrEmpty(featureId);

        var features = GetAlwaysEnabledFeatures(settings);

        if (features.RemoveAll(feature => string.Equals(feature, featureId, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            WriteArray(settings, TenantHierarchyConstants.SettingsKeys.Features, features);
        }
    }

    /// <summary>
    /// Returns the features the tenant configuration always enables.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    public static List<string> GetAlwaysEnabledFeatures(ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.ShellConfiguration
            .GetSection(TenantHierarchyConstants.SettingsKeys.Features)
            .GetChildren()
            .Select(section => section.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    /// <summary>
    /// Removes every tenant hierarchy key from the shell settings.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    public static void ClearHierarchy(ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ClearSection(settings, TenantHierarchyConstants.SettingsKeys.Prefix);
        RemoveAlwaysEnabledFeature(settings, TenantHierarchyConstants.Features.Parent);
        RemoveAlwaysEnabledFeature(settings, TenantHierarchyConstants.Features.Child);
    }

    private static void ClearSection(ShellSettings settings, string key)
    {
        var keys = settings.ShellConfiguration
            .GetSection(key)
            .AsEnumerable()
            .Select(pair => pair.Key)
            .Where(candidate => !string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var candidate in keys)
        {
            settings[candidate] = null;
        }
    }

    private static string NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
