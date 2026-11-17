using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Agent.Roles;

/// <summary>
/// A permission an enabled feature provides, with where the Roles admin page lists it.
/// </summary>
/// <param name="Permission">The permission.</param>
/// <param name="Group">The category of the permission, or the name of the feature that provides it.</param>
/// <param name="FeatureId">The ID of the feature that provides the permission.</param>
internal sealed record InstalledPermission(Permission Permission, string Group, string FeatureId);
