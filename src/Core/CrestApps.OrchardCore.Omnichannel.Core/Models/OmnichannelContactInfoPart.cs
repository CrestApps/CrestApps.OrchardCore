using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the omnichannel contact info part.
/// </summary>
public sealed class OmnichannelContactInfoPart : ContentPart
{
    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    public TextField FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name.
    /// </summary>
    public TextField LastName { get; set; }
}
