namespace CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;

/// <summary>
/// The edit view model for an <c>MessageTemplate</c>.
/// </summary>
public class MessageTemplateViewModel
{
    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the template body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the kind of work the template is for; empty for the messaging workspace.
    /// </summary>
    public string Scope { get; set; }

    public bool FillsPlaceholders { get; set; }

    /// <summary>
    /// Gets or sets the scopes other modules registered, by name, with their titles; empty hides the choice.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public IList<KeyValuePair<string, string>> Scopes { get; set; } = [];
}
