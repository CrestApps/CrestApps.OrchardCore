using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Json.Schema;

namespace CrestApps.OrchardCore.Surveys.Core.Schemas;

/// <summary>
/// Builds the JSON schema of a survey definition, with the settings of every registered question type, so authors and
/// AI tools can write survey recipes.
/// </summary>
public sealed class SurveyDefinitionSchemaBuilder
{
    private readonly ISurveyControlRegistry _registry;
    private readonly IEnumerable<ISurveyControlSchemaProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyDefinitionSchemaBuilder"/> class.
    /// </summary>
    /// <param name="registry">The question type registry.</param>
    /// <param name="providers">The settings schema providers.</param>
    public SurveyDefinitionSchemaBuilder(
        ISurveyControlRegistry registry,
        IEnumerable<ISurveyControlSchemaProvider> providers)
    {
        _registry = registry;
        _providers = providers;
    }

    /// <summary>
    /// Builds the schema of a survey definition.
    /// </summary>
    /// <returns>The schema.</returns>
    public JsonSchemaBuilder BuildDefinition()
        => SurveyModelSchemaBuilder.Build(typeof(SurveyDefinition), SettingsSchemas())
            .Description("A survey definition: pages, questions, logic, endings and versioned settings. Question ids, choice ids and names never change once responses exist. Question types: " + string.Join(", ", _registry.GetControls().Select(control => control.Name)) + ".");

    /// <summary>
    /// Builds the schema of a question, as a bank holds it.
    /// </summary>
    /// <returns>The schema.</returns>
    public JsonSchemaBuilder BuildQuestion()
        => SurveyModelSchemaBuilder.Build(typeof(SurveyQuestion), SettingsSchemas());

    private List<JsonSchemaBuilder> SettingsSchemas()
    {
        var fallback = new ReflectionSurveyControlSchemaProvider();
        var schemas = new List<JsonSchemaBuilder>();

        foreach (var control in _registry.GetControls())
        {
            var json = _providers.Select(provider => provider.GetSettingsSchema(control)).FirstOrDefault(schema => schema is not null)
                ?? fallback.GetSettingsSchema(control);

            var builder = new JsonSchemaBuilder();

            foreach (var (keyword, value) in json)
            {
                builder.Add(keyword, value?.DeepClone());
            }

            schemas.Add(builder);
        }

        return schemas;
    }
}
