using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Surveys.Core.Schemas;
using CrestApps.OrchardCore.Surveys.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Describes the SurveyTemplates recipe step.
/// </summary>
internal sealed class SurveyTemplatesRecipeStepSchema : RecipeStepSchemaBase
{
    private readonly SurveyDefinitionSchemaBuilder _definitionSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTemplatesRecipeStepSchema"/> class.
    /// </summary>
    /// <param name="definitionSchema">The survey definition schema builder.</param>
    public SurveyTemplatesRecipeStepSchema(SurveyDefinitionSchemaBuilder definitionSchema)
    {
        _definitionSchema = definitionSchema;
    }

    /// <inheritdoc />
    public override string Name => SurveysRecipeSteps.Templates;

    /// <inheritdoc />
    protected override JsonSchema CreateSchema()
    {
        var entry = SurveyModelSchemaBuilder.Build(typeof(SurveyTemplate), [], nameof(SurveyTemplate.Definition))
            .Properties(
                (nameof(SurveyTemplate.Name), new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The unique technical name of the template; an existing template of that name is updated.")),
                (nameof(SurveyTemplate.Definition), _definitionSchema.BuildDefinition().Description("The survey definition new surveys copy, with new ids.")))
            .Required(nameof(SurveyTemplate.Name));

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [(SurveysRecipeSteps.TemplatesSection, RecipeStepSchemaBuilders.Array(entry).Description("The survey templates, matched by id, then by name."))],
            [SurveysRecipeSteps.TemplatesSection]);
    }
}
