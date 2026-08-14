using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the <c>Settings</c> recipe step.
/// </summary>
public sealed class SettingsRecipeStep(IEnumerable<ISiteSettingsSchemaDefinition> schemaDefinitions) : IRecipeStep
{
    private readonly IEnumerable<ISiteSettingsSchemaDefinition> _schemaDefinitions = schemaDefinitions;

    private JsonSchema _cached;

    public string Name => "settings";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var properties = CreateBaseProperties();

        foreach (var schemaDefinition in _schemaDefinitions.OrderBy(definition => definition.Name, StringComparer.Ordinal))
        {
            properties[schemaDefinition.Name] = await schemaDefinition.GetSchemaAsync(cancellationToken);
        }

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(properties)
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return _cached;
    }

    private static Dictionary<string, JsonSchemaBuilder> CreateBaseProperties()
    {
        return new Dictionary<string, JsonSchemaBuilder>(StringComparer.Ordinal)
        {
            ["name"] = RecipeStepSchemaBuilders.String().Const("settings").Description("Recipe step discriminator. Must be 'settings'."),
            ["BaseUrl"] = RecipeStepSchemaBuilders.Nullable(RecipeStepSchemaBuilders.String()).Description("Optional absolute base URL for the site."),
            ["Calendar"] = RecipeStepSchemaBuilders.Nullable(RecipeStepSchemaBuilders.String()).Description("Optional calendar system identifier used by the site."),
            ["MaxPagedCount"] = RecipeStepSchemaBuilders.Integer().Description("Maximum number of items Orchard may return from paged queries."),
            ["MaxPageSize"] = RecipeStepSchemaBuilders.Integer().Description("Maximum page size allowed by Orchard paging APIs."),
            ["PageSize"] = RecipeStepSchemaBuilders.Integer().Description("Default page size used by Orchard lists and queries."),
            ["ResourceDebugMode"] = RecipeStepSchemaBuilders.String().Enum(["FromConfiguration", "Enabled", "Disabled"]).Description("Controls whether resource debugging stays with configuration, is forced on, or is forced off."),
            ["SiteName"] = RecipeStepSchemaBuilders.String().Description("Public site name."),
            ["PageTitleFormat"] = RecipeStepSchemaBuilders.String().Description("Page title format string, typically including placeholders like '{{ Site.SiteName }}'."),
            ["SiteSalt"] = RecipeStepSchemaBuilders.String().Description("Application salt used by Orchard security and hashing features."),
            ["SuperUser"] = RecipeStepSchemaBuilders.String().Description("User name of the Orchard super user account."),
            ["TimeZoneId"] = RecipeStepSchemaBuilders.String().Description("Default site time zone ID."),
            ["UseCdn"] = RecipeStepSchemaBuilders.Boolean().Description("Whether Orchard should serve static assets from a CDN base URL."),
            ["CdnBaseUrl"] = RecipeStepSchemaBuilders.Nullable(RecipeStepSchemaBuilders.String()).Description("CDN base URL used when UseCdn is enabled."),
            ["AppendVersion"] = RecipeStepSchemaBuilders.Boolean().Description("Whether Orchard should append file version hashes to resource URLs."),
            ["HomeRoute"] = RecipeStepSchemaBuilders.Nullable(BuildHomeRoute()).Description("Optional route object that overrides the site's home page target."),
            ["CacheMode"] = RecipeStepSchemaBuilders.String().Enum(["FromConfiguration", "Enabled", "DebugEnabled", "Disabled"]).Description("Controls Orchard cache behavior for the tenant."),
        };
    }

    private static JsonSchemaBuilder BuildHomeRoute()
        => RecipeStepSchemaBuilders.Object(
        [
            ("Area", RecipeStepSchemaBuilders.String().Description("ASP.NET Core route area.")),
            ("Controller", RecipeStepSchemaBuilders.String().Description("ASP.NET Core route controller.")),
            ("Action", RecipeStepSchemaBuilders.String().Description("ASP.NET Core route action.")),
        ],
        requiredProperties: ["Area", "Controller", "Action"],
        additionalProperties: true);
}
