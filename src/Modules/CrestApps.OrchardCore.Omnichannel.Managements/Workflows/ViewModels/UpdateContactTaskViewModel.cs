namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;

/// <summary>
/// Represents the editor of the Update Contact workflow task.
/// </summary>
public class UpdateContactTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression that resolves the contact's or lead's content item id.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression that renders the JSON merged into the record.
    /// </summary>
    public string ContentProperties { get; set; }

    /// <summary>
    /// Gets or sets whether a record whose latest version is a draft is published once changed.
    /// </summary>
    public bool Publish { get; set; }
}
