using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Composes the deployment step schemas from the deployment step factories available on the tenant and the
/// registered <see cref="IDeploymentStepSchemaDefinition"/> contributions into the schema used by the
/// <c>deployment</c> recipe step.
/// </summary>
public sealed class DeploymentSchemaService : IDeploymentSchemaService
{
    private readonly IEnumerable<IDeploymentStepFactory> _stepFactories;
    private readonly IEnumerable<IDeploymentStepSchemaDefinition> _definitions;
    private readonly IRecipeSchemaExampleService _exampleService;

    private IReadOnlyList<DeploymentStepDescriptor> _cachedDescriptors;
    private JsonSchemaBuilder _cachedStepSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentSchemaService"/> class.
    /// </summary>
    /// <param name="stepFactories">The deployment step factories registered on the tenant.</param>
    /// <param name="definitions">The registered deployment step schema definitions.</param>
    /// <param name="exampleService">The service that supplies live tenant example values.</param>
    public DeploymentSchemaService(
        IEnumerable<IDeploymentStepFactory> stepFactories,
        IEnumerable<IDeploymentStepSchemaDefinition> definitions,
        IRecipeSchemaExampleService exampleService)
    {
        _stepFactories = stepFactories;
        _definitions = definitions;
        _exampleService = exampleService;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DeploymentStepDescriptor>> GetStepDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedDescriptors is not null)
        {
            return _cachedDescriptors;
        }

        var definitions = _definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.StepType))
            .GroupBy(definition => definition.StepType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var stepTypes = _stepFactories
            .Select(factory => factory.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

        var descriptors = new List<DeploymentStepDescriptor>();

        var examples = await _exampleService.GetExamplesAsync(cancellationToken);

        foreach (var stepType in stepTypes)
        {
            if (!definitions.TryGetValue(stepType, out var definition))
            {
                descriptors.Add(new DeploymentStepDescriptor
                {
                    StepType = stepType,
                    HasSchemaDefinition = false,
                });

                continue;
            }

            var context = new DeploymentStepSchemaContext
            {
                StepType = stepType,
                Examples = examples,
            };

            var schema = await definition.GetStepSchemaAsync(context, cancellationToken);

            descriptors.Add(new DeploymentStepDescriptor
            {
                StepType = stepType,
                DisplayText = schema?.DisplayText,
                Description = schema?.Description,
                HasSchemaDefinition = schema is not null,
                Properties = schema?.Properties ?? [],
                RequiredProperties = schema?.RequiredProperties ?? [],
                AllowAdditionalProperties = schema?.AllowAdditionalProperties ?? true,
            });
        }

        _cachedDescriptors = descriptors;

        return _cachedDescriptors;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchemaBuilder> GetStepSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedStepSchema is not null)
        {
            return _cachedStepSchema;
        }

        var descriptors = await GetStepDescriptorsAsync(cancellationToken);

        var stepTypes = descriptors
            .Select(descriptor => descriptor.StepType)
            .ToArray();

        var conditionals = new List<JsonSchemaBuilder>();

        foreach (var descriptor in descriptors.Where(descriptor => descriptor.HasSchemaDefinition))
        {
            var stepProperties = descriptor.Properties.ToArray();

            var stepBuilder = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Description(BuildStepDescription(descriptor));

            if (stepProperties.Length > 0)
            {
                stepBuilder = stepBuilder.Properties(stepProperties);
            }

            if (descriptor.RequiredProperties.Count > 0)
            {
                stepBuilder = stepBuilder.Required(descriptor.RequiredProperties.ToArray());
            }

            stepBuilder = stepBuilder.AdditionalProperties(descriptor.AllowAdditionalProperties);

            conditionals.Add(new JsonSchemaBuilder()
                .If(new JsonSchemaBuilder()
                    .Properties(("Type", new JsonSchemaBuilder().Const(descriptor.StepType)))
                    .Required("Type"))
                .Then(new JsonSchemaBuilder()
                    .Properties(("Step", stepBuilder))));
        }

        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Type", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(stepTypes)
                    .Description("The deployment step type name, which is the step's CLR type name, for example ContentDeploymentStep. The well known payload of 'Step' depends on this value.")),
                ("Step", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)
                    .Description("The deployment step payload. The well known members depend on the value of 'Type'. Marker steps that export everything of a kind use an empty object.")))
            .Required("Type", "Step")
            .AdditionalProperties(true)
            .Description("A deployment step that belongs to the plan.");

        if (conditionals.Count > 0)
        {
            builder = builder.AllOf(conditionals.ToArray());
        }

        _cachedStepSchema = builder;

        return _cachedStepSchema;
    }

    private static string BuildStepDescription(DeploymentStepDescriptor descriptor)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(descriptor.DisplayText))
        {
            parts.Add($"{descriptor.DisplayText} step.");
        }

        if (!string.IsNullOrWhiteSpace(descriptor.Description))
        {
            parts.Add(descriptor.Description.EndsWith('.') ? descriptor.Description : $"{descriptor.Description}.");
        }

        parts.Add($"Payload for the '{descriptor.StepType}' deployment step.");

        return string.Join(" ", parts);
    }
}
