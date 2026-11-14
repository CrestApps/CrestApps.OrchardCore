using CrestApps.Core.Support;
using CrestApps.OrchardCore.Core.Liquid;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessageTemplateRenderer"/>: renders a template's body with Liquid when the Liquid feature is
/// on, reading each model as plain values (<c>{{ Contact.DisplayName }}</c>, <c>{{ Ticket.Number }}</c>). A body that
/// fails to render, or a site without Liquid, gives the body as written.
/// </summary>
public sealed class LiquidMessageTemplateRenderer : IMessageTemplateRenderer
{
    private readonly ILiquidTemplateManager _liquid;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiquidMessageTemplateRenderer"/> class.
    /// </summary>
    /// <param name="liquidTemplateManagers">The Liquid template manager, when the Liquid feature is on.</param>
    /// <param name="logger">The logger.</param>
    public LiquidMessageTemplateRenderer(IEnumerable<ILiquidTemplateManager> liquidTemplateManagers, ILogger<LiquidMessageTemplateRenderer> logger)
    {
        _liquid = liquidTemplateManagers?.FirstOrDefault();
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> RenderAsync(MessageTemplate template, IReadOnlyDictionary<string, object> models, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        var body = template.Body ?? string.Empty;

        // Only a body with Liquid in it is rendered, so a plain reply never changes.
        if (!body.Contains("{{", StringComparison.Ordinal) && !body.Contains("{%", StringComparison.Ordinal))
        {
            return body;
        }

        if (_liquid is not { } liquid)
        {
            return body;
        }

        try
        {
            var values = new Dictionary<string, FluidValue>(StringComparer.Ordinal);

            foreach (var (name, model) in models ?? new Dictionary<string, object>())
            {
                values[name] = model is string text ? StringValue.Create(text) : LiquidModelValues.Create(model);
            }

            // A reply is plain text, so nothing in it is encoded as HTML.
            return (await liquid.RenderStringAsync(body, NullEncoder.Default, values)).Trim();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The reply template '{TemplateId}' couldn't render; its body was used as written.", template.ItemId.SanitizeLogValue());

            return body;
        }
    }
}
