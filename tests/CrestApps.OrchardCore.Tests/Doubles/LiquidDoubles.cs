using System.Text.Encodings.Web;
using Fluid;
using Fluid.Values;
using Moq;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A Liquid template manager that renders with Fluid itself, so a test reads what a store's template really gives.
/// </summary>
public static class LiquidDoubles
{
    private static readonly FluidParser _parser = new();

    /// <summary>
    /// Returns a template manager that parses and renders each template with Fluid.
    /// </summary>
    public static ILiquidTemplateManager Manager()
    {
        var liquid = new Mock<ILiquidTemplateManager>();

        liquid
            .Setup(value => value.RenderStringAsync(It.IsAny<string>(), It.IsAny<TextEncoder>(), It.IsAny<object>(), It.IsAny<IEnumerable<KeyValuePair<string, FluidValue>>>()))
            .Returns<string, TextEncoder, object, IEnumerable<KeyValuePair<string, FluidValue>>>(RenderAsync);

        return liquid.Object;
    }

    private static async Task<string> RenderAsync(string source, TextEncoder encoder, object model, IEnumerable<KeyValuePair<string, FluidValue>> properties)
    {
        var template = _parser.Parse(source);
        var context = new TemplateContext(model ?? new object(), TemplateOptions.Default);

        foreach (var (name, value) in properties ?? [])
        {
            context.SetValue(name, value);
        }

        return await template.RenderAsync(context, encoder);
    }
}
