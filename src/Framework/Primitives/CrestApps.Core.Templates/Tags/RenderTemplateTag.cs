using System.Text.Encodings.Web;
using CrestApps.Core.Templates.Services;
using Fluid;
using Fluid.Ast;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.Templates.Tags;

/// <summary>
/// Custom Fluid tag that renders another template by ID within the current Liquid scope.
/// <para>
/// The sub-template is rendered in a child scope that inherits all variables from the
/// calling template. This enables template composition where parent variables are accessible
/// in the included template, but variables defined in the sub-template do not leak upward.
/// </para>
/// <para>
/// Usage: <c>{% render_ai_template "template-id" %}</c>
/// </para>
/// <para>
/// The template ID can also be a variable: <c>{% render_ai_template myTemplateId %}</c>
/// </para>
/// </summary>
public static class RenderTemplateTag
{
    /// <summary>
    /// The Liquid tag name used in templates.
    /// </summary>
    public const string TagName = "render_ai_template";

    internal const string AmbientFluidParserKey = "FluidParser";

    private const int MaxRecursionDepth = 10;
    private const string RecursionDepthKey = "__render_ai_template_depth";
    /// <summary>
    /// Tag handler invoked by the Fluid engine when <c>{% render_ai_template "id" %}</c> is encountered.
    /// </summary>
    public static async ValueTask<Completion> WriteToAsync(
        Expression expression,
        TextWriter writer,
        TextEncoder encoder,
        TemplateContext context)
    {
        var templateId = (await expression.EvaluateAsync(context)).ToStringValue();

        if (string.IsNullOrWhiteSpace(templateId))
        {
            return Completion.Normal;
        }

        // Guard against infinite recursion.
        var depth = 0;

        if (context.AmbientValues.TryGetValue(RecursionDepthKey, out var depthObj) && depthObj is int d)
        {
            depth = d;
        }

        if (depth >= MaxRecursionDepth)
        {
            return Completion.Normal;
        }

        if (!context.AmbientValues.TryGetValue("ServiceProvider", out var sp) ||
            sp is not IServiceProvider serviceProvider)
        {
            return Completion.Normal;
        }

        var service = serviceProvider.GetService<ITemplateService>();

        if (service == null)
        {
            return Completion.Normal;
        }

        var template = await service.GetAsync(templateId);

        if (template == null || string.IsNullOrWhiteSpace(template.Content))
        {
            return Completion.Normal;
        }

        if (!context.AmbientValues.TryGetValue(AmbientFluidParserKey, out var parserObj) ||
            parserObj is not FluidParser parser)
        {
            return Completion.Normal;
        }

        if (!parser.TryParse(template.Content, out var fluidTemplate, out _))
        {
            return Completion.Normal;
        }

        // Render in a child scope so that parent variables are accessible
        // but new variables defined in the sub-template do not leak upward.
        context.AmbientValues[RecursionDepthKey] = depth + 1;
        context.EnterChildScope();

        try
        {
            await fluidTemplate.RenderAsync(writer, encoder, context);
        }
        finally
        {
            context.ReleaseScope();
            context.AmbientValues[RecursionDepthKey] = depth;
        }

        return Completion.Normal;
    }
}
