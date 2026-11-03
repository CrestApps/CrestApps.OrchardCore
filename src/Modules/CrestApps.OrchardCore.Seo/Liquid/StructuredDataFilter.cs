using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Seo.Liquid;

/// <summary>
/// The <c>structured_data</c> Liquid filter: adds the JSON-LD it is given to the page's structured data and writes
/// nothing, so a Liquid template, such as a theme's page or a Template, can describe its page.
/// </summary>
/// <example>
/// <code>
/// {% capture page_json_ld %}{ "@type": "AboutPage", "name": "About {{ Site.SiteName }}" }{% endcapture %}
/// {{ page_json_ld | structured_data }}
/// </code>
/// </example>
internal sealed class StructuredDataFilter : ILiquidFilter
{
    /// <inheritdoc/>
    public ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var json = input?.ToStringValue();

        if (!string.IsNullOrWhiteSpace(json) &&
            !context.Services.GetRequiredService<IPageStructuredData>().AddJsonLd(json))
        {
            context.Services.GetRequiredService<ILogger<StructuredDataFilter>>()
                .LogWarning("The JSON-LD given to the structured_data Liquid filter is not valid JSON and is ignored.");
        }

        return ValueTask.FromResult<FluidValue>(StringValue.Empty);
    }
}
