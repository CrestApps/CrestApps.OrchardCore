using CrestApps.Core.Support;
using CrestApps.OrchardCore.Core.Liquid;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessageTemplateRenderer"/>: fills in a template's placeholders with restricted Liquid, which
/// reads only the models handed to it as plain values (<c>{{ Contact.DisplayName }}</c>, <c>{{ Ticket.Number }}</c>) and
/// never the request, the user or the site. A plain-text template, or a body that doesn't parse or fails to render, gives
/// the body as written.
/// </summary>
public sealed class LiquidMessageTemplateRenderer : IMessageTemplateRenderer
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiquidMessageTemplateRenderer"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public LiquidMessageTemplateRenderer(ILogger<LiquidMessageTemplateRenderer> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> RenderAsync(MessageTemplate template, IReadOnlyDictionary<string, object> models, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        var body = template.Body ?? string.Empty;

        // Only a body with Liquid in it is rendered, so a plain reply never changes.
        if (template.IsPlainText || !RestrictedLiquid.HasLiquid(body))
        {
            return body;
        }

        try
        {
            var values = new Dictionary<string, FluidValue>(StringComparer.Ordinal);

            foreach (var (name, model) in models ?? new Dictionary<string, object>())
            {
                values[name] = model is string text ? StringValue.Create(text) : LiquidModelValues.Create(model, RestrictedLiquid.Options);
            }

            // A reply is plain text, so nothing in it is encoded as HTML.
            if (await RestrictedLiquid.RenderAsync(body, values, NullEncoder.Default) is { } rendered)
            {
                return rendered.Trim();
            }

            _logger.LogWarning("The reply template '{TemplateId}' doesn't parse; its body was used as written.", template.ItemId.SanitizeLogValue());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The reply template '{TemplateId}' couldn't render; its body was used as written.", template.ItemId.SanitizeLogValue());
        }

        return body;
    }
}
