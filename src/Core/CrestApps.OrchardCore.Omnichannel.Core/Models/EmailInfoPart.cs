using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the email info part.
/// </summary>
public sealed class EmailInfoPart : ContentPart
{
    /// <summary>
    /// Gets or sets the email.
    /// </summary>
    public TextField Email { get; set; }
}
