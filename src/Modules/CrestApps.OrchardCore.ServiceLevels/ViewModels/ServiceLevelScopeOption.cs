namespace CrestApps.OrchardCore.ServiceLevels.ViewModels;

/// <summary>
/// A scope a new policy can be created for.
/// </summary>
/// <param name="Name">The scope's name.</param>
/// <param name="DisplayName">The scope's name shown to users.</param>
/// <param name="Description">What the scope's policies are for, if it says.</param>
public sealed record ServiceLevelScopeOption(string Name, string DisplayName, string Description = null);
