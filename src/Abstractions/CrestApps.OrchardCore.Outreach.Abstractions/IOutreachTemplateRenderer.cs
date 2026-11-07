using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Outreach;

/// <summary>
/// Renders an outreach run's template for one recipient, such as with Liquid and the recipient's own unsubscribe link.
/// </summary>
public interface IOutreachTemplateRenderer
{
    /// <summary>
    /// Gets the name runs refer to the renderer by, in <see cref="OutreachRun.Renderer"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Renders the run's template for a recipient.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="recipient">The recipient.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The message for the recipient.</returns>
    Task<OutreachContent> RenderAsync(OutreachRun run, OutreachRecipient recipient, CancellationToken cancellationToken = default);
}
