using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>RecipeFileDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class RecipeFileDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "RecipeFileDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Recipe metadata";

    /// <inheritdoc />
    protected override string Description => "Writes the recipe metadata, such as name, author and version, into the exported recipe file.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("RecipeName", DeploymentSchemaBuilders.String("The technical recipe name."));
        yield return ("DisplayName", DeploymentSchemaBuilders.String("The human readable recipe name."));
        yield return ("Description", DeploymentSchemaBuilders.String("A description explaining what the recipe does."));
        yield return ("Author", DeploymentSchemaBuilders.String("The recipe author."));
        yield return ("WebSite", DeploymentSchemaBuilders.String("A website URL associated with the recipe."));
        yield return ("Version", DeploymentSchemaBuilders.String("The recipe version."));
        yield return ("IsSetupRecipe", DeploymentSchemaBuilders.Boolean("Whether the recipe can be used to set up a new tenant."));
        yield return ("Categories", DeploymentSchemaBuilders.String("A comma separated list of categories the recipe belongs to."));
        yield return ("Tags", DeploymentSchemaBuilders.String("A comma separated list of tags describing the recipe."));
    }
}
