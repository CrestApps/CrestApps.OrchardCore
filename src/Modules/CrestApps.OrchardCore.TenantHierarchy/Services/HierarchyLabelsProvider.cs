using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Resolves the words the current parent tenant shows for the hierarchy.
/// </summary>
public sealed class HierarchyLabelsProvider
{
    private readonly ShellSettings _shellSettings;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchyLabelsProvider"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public HierarchyLabelsProvider(
        ShellSettings shellSettings,
        IStringLocalizer<HierarchyLabelsProvider> stringLocalizer)
    {
        _shellSettings = shellSettings;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the labels of the current tenant's policy, with the defaults filled in.
    /// </summary>
    public HierarchyLabels GetLabels()
        => Resolve(_shellSettings.IsParentTenant() ? _shellSettings.GetParentPolicy().Labels : null);

    /// <summary>
    /// Returns the labels of a policy, with the defaults filled in.
    /// </summary>
    /// <param name="labels">The labels of the policy, or <see langword="null"/>.</param>
    public HierarchyLabels Resolve(TenantHierarchyLabels labels)
    {
        return new HierarchyLabels
        {
            Parent = Pick(labels?.Parent, S["Parent tenant"]),
            Child = Pick(labels?.Child, S["Child tenant"]),
            Children = Pick(labels?.Children, S["Child tenants"]),
        };
    }

    private static string Pick(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
