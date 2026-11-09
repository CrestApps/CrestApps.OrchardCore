using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// Sends a run's template as it is, the same to every recipient.
/// </summary>
public sealed class TextOutreachTemplateRenderer : IOutreachTemplateRenderer
{
    /// <inheritdoc/>
    public string Name
        => OutreachConstants.TextRenderer;

    /// <inheritdoc/>
    public Task<OutreachContent> RenderAsync(OutreachRun run, OutreachRecipient recipient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var template = run.Template ?? new OutreachTemplate();

        return Task.FromResult(new OutreachContent
        {
            Subject = template.Subject,
            Body = template.Body,
            IsHtml = template.IsHtml,
        });
    }
}
