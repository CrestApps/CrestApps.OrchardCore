using CrestApps.AI.Prompting.Services;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.AI.Prompting.Tags;

/// <summary>
/// Fluid filter that includes the rendered content of another prompt template.
/// Usage: {{ "prompt-id" | include_prompt }}
/// </summary>
public static class IncludeTemplateFilter
{
    public const string FilterName = "include_prompt";

    public static ValueTask<FluidValue> IncludePromptAsync(
        FluidValue input,
        FilterArguments arguments,
        TemplateContext context)
    {
        return ResolveAsync(input, context);
    }

    private static async ValueTask<FluidValue> ResolveAsync(FluidValue input, TemplateContext context)
    {
        var promptId = input?.ToStringValue();

        if (string.IsNullOrWhiteSpace(promptId))
        {
            return NilValue.Instance;
        }

        if (context.AmbientValues.TryGetValue("ServiceProvider", out var sp) &&
            sp is IServiceProvider serviceProvider)
        {
            var service = serviceProvider.GetService<IAITemplateService>();
            if (service != null)
            {
                var rendered = await service.RenderAsync(promptId);
                if (rendered != null)
                {
                    return new StringValue(rendered);
                }
            }
        }

        return NilValue.Instance;
    }
}
