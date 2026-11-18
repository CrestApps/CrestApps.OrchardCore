using CrestApps.OrchardCore.AdminTheme.Deployments;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.AdminTheme.Recipes;

/// <summary>
/// Describes the 'AdminColorSchemes' recipe step to CrestApps Recipes.
/// </summary>
internal sealed class AdminColorSchemesRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => AdminColorSchemesSteps.StepName;

    protected override string EntriesName => AdminColorSchemesSteps.EntriesName;

    protected override string Description => "The color schemes of the CrestApps admin theme (its defaults are seeded with the ids gray, blue, ocean, forest, plum, sunset, graphite and gold).";

    protected override string EntryDescription => "A color scheme: a name and four colors, from which every color of the admin is derived, light and dark, meeting WCAG 2.2 AA contrast.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name", "Primary", "Deep", "Ink", "Accent"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MaxLength(60).Description("The name shown in the pickers.")),
            ("Primary", Color("Actions, links and the current page.")),
            ("Deep", Color("Headings and the top of the menu; the hue of the scheme's surfaces is taken from it.")),
            ("Ink", Color("The bottom of the menu, the darkest surface.")),
            ("Accent", Color("Links and highlights on dark surfaces.")),
            ("LightMenu", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the menu is light gray with dark text, like TheAdmin's. Defaults to false.")),
            ("Position", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Description("Where the scheme sits in the pickers, lowest first. After the existing schemes when omitted.")),
            ("Hue", new JsonSchemaBuilder().Type(SchemaValueType.Number).Minimum(0).Maximum(360).Description("Optional: the hue of the page, borders and dark mode surfaces. The deep color's when omitted.")),
            ("Saturation", new JsonSchemaBuilder().Type(SchemaValueType.Number).Minimum(0).Maximum(1).Description("Optional: how colorful those surfaces are, 0 (gray) to 1. Worked out from the deep color when omitted.")),
            ("OnPrimary", Color("Optional: the text of buttons and of the current page. White, or a dark color on a light primary, when omitted.")),
            ("Page", Color("Optional: the page in light mode. A pale tone of the hue when omitted.")),
            ("Toolbar", Color("Optional: the bar above the records of a list. The primary when omitted.")),
            ("Tint", Color("Optional: both ends of the page gradient in light mode. The primary and the accent when omitted.")),
            ("TintDark", Color("Optional: both ends of the page gradient in dark mode. The accent and the primary when omitted.")),
        ];

    private static JsonSchemaBuilder Color(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern("^#[0-9a-fA-F]{6}$").Description(description + " As '#rrggbb'.");
}

/// <summary>
/// Describes the 'AdminColorSchemes' deployment step to CrestApps Recipes.
/// </summary>
internal sealed class AdminColorSchemesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(AdminColorSchemesDeploymentStep);

    protected override string DisplayText => "Admin color schemes";

    protected override string Description => "Exports every color scheme of the CrestApps admin theme.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
