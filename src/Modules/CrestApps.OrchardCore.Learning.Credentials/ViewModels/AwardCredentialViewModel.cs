using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Learning.Credentials.ViewModels;

/// <summary>
/// The form that awards a credential by hand.
/// </summary>
public class AwardCredentialViewModel
{
    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    public string DefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the recipient's user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets why it is awarded.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the published definitions.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Definitions { get; set; } = [];

    /// <summary>
    /// Gets or sets the editor the display drivers build: its fields in cards.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }
}
