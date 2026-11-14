using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// A reusable reply (canned response) an agent drops into a composer: a named body of text, with Liquid placeholders
/// such as <c>{{ Contact.DisplayName }}</c> filled in by <see cref="Services.IMessageTemplateRenderer"/>, offered where
/// its scope is answered.
/// </summary>
public sealed class MessageTemplate : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
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
    /// Gets or sets the kind of work the template is for, such as <c>Tickets</c>, one of
    /// <see cref="MessageTemplateScopeOptions.Scopes"/>; empty for the messaging workspace.
    /// </summary>
    public string Scope { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the body is inserted as written, without filling in placeholders. Templates
    /// written before placeholders existed are plain text, so braces an agent typed keep their meaning.
    /// </summary>
    public bool IsPlainText { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the template was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the template was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
