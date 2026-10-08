using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Lets a module finish a profile that was built from a template, for example by copying data the template
/// owns so the new profile gets its own.
/// </summary>
/// <remarks>
/// Handlers run only when a profile built from a template is about to be persisted. They never run while the
/// profile editor is prefilled from a template, so a form that is never saved leaves nothing behind.
/// </remarks>
public interface IAIProfileTemplateApplicationHandler
{
    /// <summary>
    /// Called after the template's values have been applied to the profile and before it is persisted.
    /// </summary>
    /// <param name="context">The profile and the template it was built from.</param>
    Task AppliedAsync(AIProfileTemplateAppliedContext context);
}
