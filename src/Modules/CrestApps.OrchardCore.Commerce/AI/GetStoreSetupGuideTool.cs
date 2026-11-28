using System.Text.Json;
using CrestApps.OrchardCore.Commerce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.AI;

/// <summary>
/// Lists the steps of Commerce › Set up your store as its page shows them (<see cref="StoreSetupGuide.DescribeAsync"/>):
/// in order, whether each is done, skipped or optional, what is set up now, the page that does it in full, and the
/// fields of its quick action. Progress is read for update, so a step skipped earlier in the same chat message shows as
/// skipped; a step run earlier in the message is reported as run, because the settings it reads to describe itself are
/// cached until the reply ends.
/// </summary>
internal sealed class GetStoreSetupGuideTool : CommerceAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getStoreSetupGuide";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the \"Set up your store\" steps in order with whether each is done, skipped or optional, what is set up now, the admin page that does it in full, and the fields its quick action takes. Use it when asked what is left before the store can sell, and before running a step.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var site = await services.GetRequiredService<ISiteService>().LoadSiteSettingsAsync();
        var progress = site.GetOrCreate<StoreSetupSettings>();
        var model = await StoreSetupGuide.DescribeAsync(services.GetServices<IStoreSetupStep>(), progress, cancellationToken);
        var ran = services.GetService<StoreSetupToolScope>()?.Ran ?? new HashSet<string>();

        var steps = model.Steps.Select(step =>
        {
            var description = step.Description;
            var ranNow = ran.Contains(step.Name);

            return new
            {
                name = step.Name,
                title = description.Title?.Value,
                description = description.Description?.Value,
                state = step.IsDone || ranNow ? "done" : step.IsSkipped ? "skipped" : "to do",
                ranInThisMessage = ranNow ? true : (bool?)null,
                optional = description.IsOptional,
                status = description.Status?.Value,
                page = description.Url,
                pageText = description.UrlText?.Value,
                quickAction = description.ActionText is null
                    ? null
                    : new
                    {
                        action = description.ActionText.Value,
                        fields = description.Fields.Select(field => new
                        {
                            name = field.Name,
                            label = field.Label?.Value,
                            type = field.Type,
                            value = field.Value,
                            required = field.Required,
                            hint = field.Hint?.Value,
                            options = field.Options.Count == 0 ? null : field.Options.Select(option => new { value = option.Key, text = option.Value }).ToArray(),
                        }).ToArray(),
                    },
            };
        }).ToArray();

        // The step to do now: the first that is neither done nor skipped, the required ones first, as on the page.
        var current = steps.FirstOrDefault(step => step.state == "to do" && !step.optional)?.name
            ?? steps.FirstOrDefault(step => step.state == "to do")?.name;

        return Json(new
        {
            steps,
            total = steps.Length,
            remaining = steps.Count(step => step.state == "to do"),
            current,
            note = ran.Count > 0
                ? "Steps run in this message are shown as done; what they set up shows in their status from the next reply."
                : null,
        });
    }
}
