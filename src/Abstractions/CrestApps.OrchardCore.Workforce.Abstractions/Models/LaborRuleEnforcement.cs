namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What happens when a labor rule is broken.
/// </summary>
public enum LaborRuleEnforcement
{
    /// <summary>
    /// The change goes ahead and the problem is shown.
    /// </summary>
    Warn,

    /// <summary>
    /// The change is refused.
    /// </summary>
    Block,

    /// <summary>
    /// The change goes ahead only with a reason from someone allowed to override, which is recorded.
    /// </summary>
    RequireOverride,
}
