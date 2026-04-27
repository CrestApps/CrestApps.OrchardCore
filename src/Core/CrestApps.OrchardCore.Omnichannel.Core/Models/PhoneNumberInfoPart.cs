using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the phone number info part.
/// </summary>
public sealed class PhoneNumberInfoPart : ContentPart
{
    /// <summary>
    /// Gets or sets the number.
    /// </summary>
    public TextField Number { get; set; }

    /// <summary>
    /// Gets or sets the extension.
    /// </summary>
    public TextField Extension { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public TextField Type { get; set; }
}
