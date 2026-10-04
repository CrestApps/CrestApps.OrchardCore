using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Sms.ViewModels;

/// <summary>
/// The AI agent a text entry point routes its texts to.
/// </summary>
public class SmsEntryPointAIAgentViewModel
{
    /// <summary>
    /// Gets or sets the AI profile that answers the texts.
    /// </summary>
    public string TargetAIProfileId { get; set; }

    /// <summary>
    /// Gets or sets where the entry point routes, posted by its routing card.
    /// </summary>
    public EntryPointTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the chat profiles that can answer.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ProfileOptions { get; set; } = [];
}
