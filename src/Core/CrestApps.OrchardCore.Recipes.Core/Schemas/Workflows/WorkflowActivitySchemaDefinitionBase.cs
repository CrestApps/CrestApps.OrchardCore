using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;

/// <summary>
/// Provides the standard implementation surface for workflow activity schema definitions.
/// </summary>
/// <remarks>
/// Derive from this class to describe a workflow event or task in the <c>WorkflowType</c> recipe step.
/// Implementations only supply the activity name, its category, its outcomes and the properties it accepts;
/// this base class assembles the <c>Properties</c> object schema and always adds the shared
/// <c>ActivityMetadata</c> property.
/// </remarks>
public abstract class WorkflowActivitySchemaDefinitionBase : IWorkflowActivitySchemaDefinition
{
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
    /// <remarks>
    /// Defaults to <see langword="true"/> because <c>ActivityRecord</c> derives from <c>Entity</c>, whose
    /// <c>Properties</c> bag is open by design. Any module can persist an additional section there through a
    /// section display driver, exactly like the built-in <c>ActivityMetadata</c> section does. The declared
    /// properties therefore document the well known members without rejecting valid payloads produced by a
    /// tenant that has extra modules enabled.
    /// </remarks>
    protected virtual bool AllowAdditionalProperties => true;

    ValueTask<WorkflowActivitySchema> IWorkflowActivitySchemaDefinition.GetActivitySchemaAsync(
        WorkflowActivitySchemaContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return BuildActivitySchemaAsync(context, cancellationToken);
    }

    /// <summary>
    /// Builds the schema describing the activity.
    /// Override this method when the schema requires asynchronous work, such as reading tenant metadata.
    /// </summary>
    /// <param name="context">The context describing the activity being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    protected virtual ValueTask<WorkflowActivitySchema> BuildActivitySchemaAsync(
        WorkflowActivitySchemaContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildActivitySchemaCore(context));

    /// <summary>
    /// Builds the property definitions accepted by the activity's <c>Properties</c> object.
    /// </summary>
    /// <remarks>
    /// The shared <c>ActivityMetadata</c> property is added automatically and must not be returned here.
    /// </remarks>
    /// <param name="context">The context describing the activity being documented.</param>
    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context);

    /// <summary>
    /// Assembles the activity schema from the declared metadata and property definitions.
    /// </summary>
    /// <remarks>
    /// Override this method to apply object level constraints that <see cref="GetPropertyDefinitions"/> cannot
    /// express, such as conditional requirements. Call the base implementation first to obtain the standard
    /// envelope, which already contains the shared <c>ActivityMetadata</c> property.
    /// </remarks>
    /// <param name="context">The context describing the activity being documented.</param>
    protected virtual WorkflowActivitySchema BuildActivitySchemaCore(WorkflowActivitySchemaContext context)
    {
        var properties = new List<(string Name, JsonSchemaBuilder Schema)>
        {
            ("ActivityMetadata", WorkflowActivitySchemaBuilders.ActivityMetadata()),
        };

        properties.AddRange(GetPropertyDefinitions(context) ?? []);

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
