using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging;
using OrchardCore.Liquid;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// The store's Liquid templates of its emails about orders: an email with a template that's on is rendered from it, with
/// the built email as <c>{{ Content }}</c>, the order as <c>{{ Order }}</c> and the email's own values; the layout goes
/// around every one. A template that fails to render sends the built email instead, so a typo never stops an order email.
/// </summary>
public sealed partial class LiquidOrderEmailTemplates : IOrderEmailTemplates
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = null, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    private static readonly TemplateOptions _options = new();

    private readonly ILiquidTemplateManager _liquid;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    public LiquidOrderEmailTemplates(ILiquidTemplateManager liquid, ISiteService siteService, ILogger<LiquidOrderEmailTemplates> logger)
    {
        _liquid = liquid;
        _siteService = siteService;
        _logger = logger;
    }

    public async Task<OrderEmailMessage> ApplyAsync(OrderEmailContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var built = new OrderEmailMessage(context.Subject, context.Text, context.Html);
        var settings = await _siteService.GetSettingsAsync<OrderEmailTemplateSettings>();
        var template = settings.Templates.TryGetValue(context.Kind ?? string.Empty, out var found) && found.IsEnabled ? found : null;

        if (template is null && string.IsNullOrWhiteSpace(settings.Layout))
        {
            return built;
        }

        try
        {
            var site = await _siteService.GetSiteSettingsAsync();
            var values = new Dictionary<string, FluidValue>(StringComparer.Ordinal)
            {
                ["Content"] = new StringValue(context.Html ?? string.Empty, false),
                ["Subject"] = new StringValue(context.Subject ?? string.Empty),
                ["StoreName"] = new StringValue(site.SiteName ?? string.Empty),
                ["Order"] = context.Order is null ? NilValue.Instance : FluidValue.Create(ToPlain(ToJson(context.Order)), _options),
            };

            foreach (var (name, value) in context.Values)
            {
                values[name] = new StringValue(value ?? string.Empty);
            }

            var subject = context.Subject;
            var html = context.Html;

            if (template is not null)
            {
                if (!string.IsNullOrWhiteSpace(template.Subject))
                {
                    subject = (await _liquid.RenderStringAsync(template.Subject, NullEncoder.Default, values)).Trim();
                }

                if (!string.IsNullOrWhiteSpace(template.Body))
                {
                    html = await _liquid.RenderStringAsync(template.Body, HtmlEncoder.Default, values);
                    values["Content"] = new StringValue(html, false);
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.Layout))
            {
                html = await _liquid.RenderStringAsync(settings.Layout, HtmlEncoder.Default, values);
            }

            // The text part follows the HTML a template wrote; with only a layout, the built text stays.
            var text = template is not null && !string.IsNullOrWhiteSpace(template.Body) ? ToText(html) : context.Text;

            return new OrderEmailMessage(string.IsNullOrWhiteSpace(subject) ? context.Subject : subject, text, html);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The store's template of the '{Kind}' email couldn't render; the built email was sent.", context.Kind);

            return built;
        }
    }

    // The order as plain values: the template reads {{ Order.OrderNumber }}, {{ Order.Lines[0].Title }} and so on.
    internal static JsonObject ToJson(Order order)
        => JsonSerializer.SerializeToNode(order, _json) as JsonObject ?? [];

    // Dictionaries, lists and values, which Liquid reads without registering a type.
    internal static object ToPlain(JsonNode node)
        => node switch
        {
            JsonObject obj => obj.ToDictionary(pair => pair.Key, pair => ToPlain(pair.Value), StringComparer.OrdinalIgnoreCase),
            JsonArray array => array.Select(ToPlain).ToList(),
            JsonValue value when value.TryGetValue<bool>(out var flag) => flag,
            JsonValue value when value.TryGetValue<decimal>(out var number) => number,
            JsonValue value => value.ToString(),
            _ => null,
        };

    internal static string ToText(string html)
        => System.Net.WebUtility.HtmlDecode(Tags().Replace(Breaks().Replace(html ?? string.Empty, "\n"), string.Empty)).Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex("<br\\s*/?>|</p>|</div>|</h[1-6]>|</li>|</tr>", RegexOptions.IgnoreCase)]
    private static partial Regex Breaks();
}
