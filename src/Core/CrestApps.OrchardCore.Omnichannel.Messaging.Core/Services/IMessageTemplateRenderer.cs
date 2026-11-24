using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Fills in a reply template's placeholders, such as <c>{{ Contact.DisplayName }}</c>, for the record a reply is for.
/// </summary>
public interface IMessageTemplateRenderer
{
    /// <summary>
    /// Returns the template's body with its Liquid placeholders filled in from the models.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="models">The records the reply is about, by the name the body reads them under, such as
    /// <c>Contact</c> or <c>Ticket</c>; a string is read as itself.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The text to send.</returns>
    Task<string> RenderAsync(MessageTemplate template, IReadOnlyDictionary<string, object> models, CancellationToken cancellationToken = default);
}
