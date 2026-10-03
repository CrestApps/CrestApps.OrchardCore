using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Telnyx.ViewModels;

/// <summary>
/// Edits the AI voice agent a call entry point hands its calls to.
/// </summary>
public class EntryPointAIAgentViewModel
{
    /// <summary>
    /// Gets or sets the AI profile that answers the calls.
    /// </summary>
    public string TargetAIProfileId { get; set; }

    /// <summary>
    /// Gets or sets the target the entry point's routing card posted, read here so the picker does not depend on the
    /// order the editors are saved in.
    /// </summary>
    public EntryPointTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the chat profiles that can answer calls.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ProfileOptions { get; set; } = [];
}
