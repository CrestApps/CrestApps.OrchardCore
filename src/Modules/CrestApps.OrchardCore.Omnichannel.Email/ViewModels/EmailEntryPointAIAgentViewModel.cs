using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Email.ViewModels;

/// <summary>
/// Edits the AI agent that answers an email entry point's mail.
/// </summary>
public class EmailEntryPointAIAgentViewModel
{
    /// <summary>
    /// Gets or sets the AI chat profile that answers the mail.
    /// </summary>
    public string TargetAIProfileId { get; set; }

    /// <summary>
    /// Gets or sets where the entry point routes, which decides whether the AI agent is kept.
    /// </summary>
    public EntryPointTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the chat profiles to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ProfileOptions { get; set; } = [];
}
