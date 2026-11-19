using System.Text.Json;
using CrestApps.OrchardCore.Commerce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.AI;

/// <summary>
/// Runs one setup step's quick action, as the step's button on Commerce › Set up your store does
/// (<see cref="StoreSetupGuide.RunAsync"/>): the step does the work and, when it succeeds, is recorded as done and no
/// longer skipped. Only the fields the step declares are accepted, required fields must be given and a choice must be one
/// the step offers (the page's form only posts those). Some steps create a content type or staff roles, so the tool asks
/// for approval.
/// </summary>
internal sealed class RunStoreSetupStepTool : CommerceAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "runStoreSetupStep";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "step": {
          "type": "string",
          "description": "The step's name, from getStoreSetupGuide, such as staff-roles."
        },
        "values": {
          "type": "object",
          "description": "The values of the quick action's fields, by field name, from getStoreSetupGuide. Leave it out for a step whose quick action has no fields.",
          "additionalProperties": { "type": ["string", "number", "boolean"] }
        }
      },
      "required": ["step"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Runs one setup step's quick action with the given field values (for example add the Product type, choose the store currency, save the store details, add a flat shipping rate, add the staff roles) and marks the step done. Only steps that report a quick action can be run; some of them create a content type or roles.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var name = ReadString(arguments, "step");
        var step = StoreSetupGuide.Find(services.GetServices<IStoreSetupStep>(), name);

        if (step is null)
        {
            return $"Nothing was done: there is no setup step named '{name}'. Use getStoreSetupGuide for the steps.";
        }

        var description = await step.DescribeAsync(cancellationToken);

        if (description.ActionText is null)
        {
            return description.Url is null
                ? $"Nothing was done: the step '{step.Name}' has no quick action."
                : $"Nothing was done: the step '{step.Name}' has no quick action. It is done on its page: {description.Url}";
        }

        var (values, error) = ReadValues(arguments, description);

        if (error is not null)
        {
            return error;
        }

        var result = await StoreSetupGuide.RunAsync(step, values, services.GetRequiredService<ISiteService>(), cancellationToken);

        if (result is null)
        {
            return $"Nothing was done: the step '{step.Name}' has no quick action.";
        }

        if (!result.Succeeded)
        {
            return $"The step '{step.Name}' was not done: {result.Message?.Value}";
        }

        services.GetService<StoreSetupToolScope>()?.RecordRun(step.Name);

        return Json(new
        {
            step = step.Name,
            title = description.Title?.Value,
            done = true,
            message = result.Message?.Value,
            page = description.Url,
        });
    }

    private static (IReadOnlyDictionary<string, string> Values, string Error) ReadValues(AIFunctionArguments arguments, StoreSetupStepDescription description)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var fields = description.Fields ?? [];

        if (ReadJson(arguments, "values") is { } given)
        {
            if (given.ValueKind != JsonValueKind.Object)
            {
                return (null, "Nothing was done: give the values as an object of field names and values.");
            }

            foreach (var property in given.EnumerateObject())
            {
                var field = fields.FirstOrDefault(entry => string.Equals(entry.Name, property.Name, StringComparison.Ordinal));

                if (field is null)
                {
                    return (null, $"Nothing was done: '{property.Name}' is not a field of this step. Its fields are: {(fields.Count == 0 ? "none" : string.Join(", ", fields.Select(entry => entry.Name)))}.");
                }

                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Null => null,
                    _ => property.Value.GetRawText(),
                };

                if (!string.IsNullOrWhiteSpace(value) && field.Options.Count > 0 && !field.Options.Any(option => string.Equals(option.Key, value.Trim(), StringComparison.Ordinal)))
                {
                    return (null, $"Nothing was done: '{value}' is not a choice of the field '{field.Name}'. Its choices are: {string.Join(", ", field.Options.Select(option => option.Key))}.");
                }

                values[field.Name] = value;
            }
        }

        var missing = fields
            .Where(field => field.Required && string.IsNullOrWhiteSpace(values.GetValueOrDefault(field.Name)) && string.IsNullOrWhiteSpace(field.Value))
            .Select(field => field.Name)
            .ToArray();

        if (missing.Length > 0)
        {
            return (null, $"Nothing was done: the step needs a value for {string.Join(", ", missing)}.");
        }

        // A field left out keeps the value the page fills in, as the page posts it.
        foreach (var field in fields)
        {
            if (!values.ContainsKey(field.Name) && field.Value is not null)
            {
                values[field.Name] = field.Value;
            }
        }

        return (values, null);
    }
}
