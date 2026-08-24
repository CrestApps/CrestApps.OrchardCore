using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Models;

/// <summary>
/// A reusable canned response (template) an agent can drop into the composer. Kept deliberately simple: a
/// named body of text.
/// </summary>
public sealed class SmsTemplate : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique template name shown in the composer picker.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the template body inserted into the composer.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the template was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the template was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
