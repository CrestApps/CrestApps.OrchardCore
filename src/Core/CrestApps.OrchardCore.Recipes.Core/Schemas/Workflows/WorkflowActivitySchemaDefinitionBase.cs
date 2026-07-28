using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;

/// <summary>
/// Provides the standard implementation surface for workflow activity schema definitions.
/// </summary>
/// <remarks>
/// Derive from this class to describe a workflow event or task in the <c>WorkflowType</c> recipe step.
/// Implementations only supply the activity name, its category, its outcomes and the properties it accepts;
/// this base class assembles the <c>Properties</c> object schema, always adds the shared
/// <c>ActivityMetadata</c> property and caches the result.
/// </remarks>
public abstract class WorkflowActivitySchemaDefinitionBase : IWorkflowActivitySchemaDefinition
{
    private WorkflowActivitySchema _cachedSchema;

    /// <summary>
    /// Gets the workflow activity name this definition describes.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the activity category. Returns <see langword="null"/> to fall back to the category reported by the
    /// activity library.
    /// </summary>
    protected virtual string Category => null;

    /// <summary>
    /// Gets the human readable activity title. Returns <see langword="null"/> to fall back to the display text
    /// reported by the activity library.
    /// </summary>
    protected virtual string DisplayText => null;

    /// <summary>
    /// Gets a description explaining what the activity does.
    /// </summary>
    protected virtual string Description => null;

    /// <summary>
    /// Gets the outcome names the activity can produce.
    /// </summary>
    protected virtual IEnumerable<string> Outcomes => [];

    /// <summary>
    /// Gets a value indicating whether the activity can produce outcomes beyond those listed in
    /// <see cref="Outcomes"/>, typically because the outcomes are derived from user supplied values.
    /// </summary>
    protected virtual bool HasDynamicOutcomes => false;

    /// <summary>
    /// Gets the names of the properties that must be provided for the activity to execute correctly.
    /// </summary>
    protected virtual IEnumerable<string> RequiredProperties => [];

    /// <summary>
    /// Gets a value indicating whether properties beyond the declared ones are accepted.
    /// </summary>
    protected virtual bool AllowAdditionalProperties => false;

    /// <inheritdoc />
    public ValueTask<WorkflowActivitySchema> GetActivitySchemaAsync(
        WorkflowActivitySchemaContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _cachedSchema ??= BuildSchema(context);

        return ValueTask.FromResult(_cachedSchema);
    }

    /// <summary>
    /// Builds the property definitions accepted by the activity's <c>Properties</c> object.
    /// </summary>
    /// <remarks>
    /// The shared <c>ActivityMetadata</c> property is added automatically and must not be returned here.
    /// </remarks>
    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions();

    private WorkflowActivitySchema BuildSchema(WorkflowActivitySchemaContext context)
    {
        var properties = new List<(string Name, JsonSchemaBuilder Schema)>
        {
            ("ActivityMetadata", WorkflowActivitySchemaBuilders.ActivityMetadata()),
        };

        properties.AddRange(GetPropertyDefinitions() ?? []);

        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(properties.ToDictionary(property => property.Name, property => property.Schema));

        var required = RequiredProperties?.ToArray() ?? [];

        if (required.Length > 0)
        {
            builder = builder.Required(required);
        }

        var outcomes = Outcomes?.ToArray() ?? [];
        var category = Category ?? context.Category;
        var description = BuildPropertiesDescription(context, category, outcomes);

        builder = builder
            .AdditionalProperties(AllowAdditionalProperties)
            .Description(description);

        return new WorkflowActivitySchema
        {
            Category = category,
            DisplayText = DisplayText ?? context.DisplayText,
            Description = Description,
            Outcomes = outcomes,
            HasDynamicOutcomes = HasDynamicOutcomes,
            Properties = builder,
        };
    }

    private string BuildPropertiesDescription(WorkflowActivitySchemaContext context, string category, string[] outcomes)
    {
        var kind = context.IsEvent
            ? "event"
            : "task";
        var parts = new List<string>
        {
            $"Properties for the '{Name}' workflow {kind}.",
        };

        if (!string.IsNullOrWhiteSpace(category))
        {
            parts.Add($"Category: {category}.");
        }

        if (!string.IsNullOrWhiteSpace(Description))
        {
            parts.Add(Description.EndsWith('.') ? Description : $"{Description}.");
        }

        if (outcomes.Length > 0)
        {
            var suffix = HasDynamicOutcomes
                ? " Additional outcomes are derived from this activity's own configuration."
                : string.Empty;

            parts.Add($"Outcomes usable as 'Transitions[].SourceOutcomeName': {string.Join(", ", outcomes)}.{suffix}");
        }
        else if (HasDynamicOutcomes)
        {
            parts.Add("Outcomes are derived from this activity's own configuration.");
        }
        else
        {
            parts.Add("This activity produces no outcomes and cannot be the source of a transition.");
        }

        return string.Join(" ", parts);
    }
}
