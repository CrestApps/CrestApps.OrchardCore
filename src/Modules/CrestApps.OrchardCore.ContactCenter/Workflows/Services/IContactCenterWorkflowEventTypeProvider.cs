using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Services;

/// <summary>
/// Supplies the selectable Contact Center domain event types for the <c>ContactCenterEvent</c> workflow
/// activity editor, so authors pick a canonical event type instead of typing a free-text name.
/// </summary>
public interface IContactCenterWorkflowEventTypeProvider
{
    /// <summary>
    /// Gets the selectable event types, grouped by domain, with a leading empty option that reacts to every event.
    /// </summary>
    /// <returns>The localized, grouped event-type options.</returns>
    IReadOnlyList<SelectListItem> GetEventTypes();
}
