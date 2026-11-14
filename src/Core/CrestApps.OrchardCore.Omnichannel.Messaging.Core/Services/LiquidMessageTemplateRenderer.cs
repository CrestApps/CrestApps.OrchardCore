using CrestApps.Core.Support;
using CrestApps.OrchardCore.Core.Liquid;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessageTemplateRenderer"/>: fills in a template's placeholders with restricted Liquid, which
/// reads only the models handed to it as plain values (<c>{{ Contact.DisplayName }}</c>, <c>{{ Ticket.Number }}</c>) and
/// never the request, the user or the site. A plain-text template, or a body that doesn't parse or fails to render, gives
/// the body as written.
/// </summary>
public sealed class LiquidMessageTemplateRenderer : IMessageTemplateRenderer
{
    private readonly ILocalClock _localClock;
    private readonly IStringLocalizer _localizer;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiquidMessageTemplateRenderer"/> class.
    /// </summary>
    /// <param name="localClock">The local clock, for the time zone dates are shown in.</param>
    /// <param name="localizer">The localizer the <c>t</c> filter looks texts up in.</param>
    /// <param name="logger">The logger.</param>
    public LiquidMessageTemplateRenderer(ILocalClock localClock, IStringLocalizer<LiquidMessageTemplateRenderer> localizer, ILogger<LiquidMessageTemplateRenderer> logger)
    {
        _localClock = localClock;
        _localizer = localizer;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> RenderAsync(MessageTemplate template, IReadOnlyDictionary<string, object> models, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        var body = template.Body ?? string.Empty;

        // Only a body with Liquid in it is rendered, so a plain reply never changes.
        if (!template.FillsPlaceholders || !RestrictedLiquid.HasLiquid(body))
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
            if (await RestrictedLiquid.RenderAsync(body, values, NullEncoder.Default, await RestrictedLiquidContexts.CreateAsync(_localClock, _localizer)) is { } rendered)
            {
                return rendered.Trim();
            }

            _logger.LogWarning("The reply template '{TemplateId}' doesn't parse or uses a filter that isn't available; its body was used as written.", template.ItemId.SanitizeLogValue());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The reply template '{TemplateId}' couldn't render; its body was used as written.", template.ItemId.SanitizeLogValue());
        }

        return body;
    }
}
