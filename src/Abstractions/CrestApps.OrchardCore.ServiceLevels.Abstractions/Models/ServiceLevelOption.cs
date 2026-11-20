namespace CrestApps.OrchardCore.ServiceLevels.Models;

/// <summary>
/// A metric or a priority a scope offers to its policies.
/// </summary>
/// <param name="Key">The stored key.</param>
/// <param name="Name">The name shown to users, already localized.</param>
public sealed record ServiceLevelOption(string Key, string Name);
