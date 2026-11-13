using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Core.Emails;
using CrestApps.OrchardCore.Core.Liquid;
using CrestApps.OrchardCore.Emails;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.Core.Emails;

/// <summary>
/// The store's own templates of its emails, kept in <see cref="OrderEmailTemplateSettings"/>
/// (<see cref="CommerceEmailTemplateSource"/>) and rendered with the site's <see cref="LiquidTemplatedEmailTemplates"/>:
/// each kind's subject and body written over in Liquid, with the built email as <c>{{ Content }}</c>, the store's name
/// as <c>{{ StoreName }}</c>, and a layout around them all.
/// </summary>
public sealed class LiquidCommerceEmailTemplates : ICommerceEmailTemplates
{
    private readonly LiquidTemplatedEmailTemplates _templates;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiquidCommerceEmailTemplates"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, to find the Liquid template manager when the Liquid feature is on.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="logger">The logger.</param>
    public LiquidCommerceEmailTemplates(IServiceProvider serviceProvider, ISiteService siteService, ILogger<LiquidCommerceEmailTemplates> logger)
    {
        _templates = new LiquidTemplatedEmailTemplates(serviceProvider, [new CommerceEmailTemplateSource(siteService)], siteService, logger);
    }

    /// <inheritdoc/>
    public async Task<CommerceEmailMessage> ApplyAsync(CommerceEmailContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = await _templates.ApplyAsync(new TemplatedEmailContext
        {
            Kind = context.Kind,
            Models = context.Models,
            Subject = context.Subject,
            Text = context.Text,
            Html = context.Html,
            Values = context.Values,
        }, cancellationToken);

        return new CommerceEmailMessage(message.Subject, message.Text, message.Html);
    }

    internal static JsonNode ToJson(object model)
        => LiquidModelValues.ToJson(model);

    internal static object ToPlain(JsonNode node)
        => LiquidModelValues.ToPlain(node);

    internal static string ToText(string html)
        => LiquidTemplatedEmailTemplates.ToText(html);
}
