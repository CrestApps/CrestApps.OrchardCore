using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.SiteResources.Recipes;

/// <summary>
/// Describes the "SiteResources" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class SiteResourcesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => SiteResourcesConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(SiteResourcesConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'SiteResources'.")),
                ("Resources", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The resource's 26-character id; omit it to create a new one.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern("^[A-Za-z0-9][A-Za-z0-9._-]*$").MaxLength(SiteResourceNames.MaxLength).Description("The unique name templates ask for and the resource's address ends with.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the resource is for.")),
                            ("Type", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Stylesheet", "Script").Description("Whether the resource is CSS or JavaScript.")),
                            ("Content", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The CSS or JavaScript as written; it is minified on import when Minify is on.")),
                            ("Minify", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the resource is served minified. True by default.")),
                            ("Target", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Frontend", "Admin", "Everywhere").Description("The pages the resource is offered to.")),
                            ("Themes", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The identifiers of the themes the resource is limited to, such as TheBlogTheme; empty or absent for every theme.")),
                            ("Placement", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Global", "OnDemand").Description("Global adds it to every page of its target; OnDemand only where a template asks for it by name.")),
                            ("Location", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Foot", "Head").Description("Where a script goes. Stylesheets always go in the head.")),
                            ("Dependencies", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The resources that must be on the page first, such as jQuery or bootstrap.")),
                            ("Position", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The order the resources added to every page are added, smallest first.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the resource is offered to pages. True by default.")))
                        .Required("Name", "Type", "Content")
                        .AdditionalProperties(true))
                    .Description("The stylesheets and scripts to create or update, matched by ItemId.")))
            .Required("name", "Resources")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
