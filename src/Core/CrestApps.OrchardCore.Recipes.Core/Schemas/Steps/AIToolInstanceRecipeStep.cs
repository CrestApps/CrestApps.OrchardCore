using CrestApps.Core.AI;
using Json.Schema;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "AIToolInstances" recipe step — creates or updates AI tool instances.
/// </summary>
public sealed class AIToolInstanceRecipeStep : IRecipeStep
{
    private readonly AIOptions _aiOptions;
    private JsonSchema _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceRecipeStep"/> class.
    /// </summary>
    /// <param name="aiOptions">The AI options.</param>
    public AIToolInstanceRecipeStep(IOptions<AIOptions> aiOptions)
    {
        _aiOptions = aiOptions.Value;
    }

    public string Name => "AIToolInstances";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private JsonSchema CreateSchema()
    {
        var sources = _aiOptions.ToolInstanceSources.Keys
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sourceSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("Tool instance source identifier that defines the shape of the instance. Required when creating a new instance.");

        if (sources.Length > 0)
        {
            sourceSchema = sourceSchema.WithSuggestions(sources);
        }

        var propertiesSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("HttpApiRequestToolSettings", BuildHttpApiRequestSettingsSchema().Description("Settings for the built-in 'http-api-request' source.")),
                ("SitemapDocumentationToolSettings", BuildSitemapSettingsSchema().Description("Settings for the built-in sitemap documentation search source.")),
                ("SearchIndexDocumentationToolSettings", BuildSearchIndexSettingsSchema().Description("Settings for the built-in prebuilt search index documentation source.")),
                ("AlgoliaDocumentationToolSettings", BuildAlgoliaSettingsSchema().Description("Settings for the built-in Algolia DocSearch documentation source.")),
                ("WebsiteSearchToolSettings", BuildWebsiteSearchSettingsSchema().Description("Settings for the built-in live website search source.")),
                ("AIToolInstanceParametersMetadata", BuildParametersMetadataSchema().Description("User-declared parameters for sources that opt into parameter support.")))
            .AdditionalProperties(true)
            .Description("Source-specific tool instance settings, grouped by settings object name. Secrets are stored encrypted at rest.");

        var instanceSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", RecipeStepSchemaBuilders.String().Description("Optional unique identifier. When supplied and found, the existing instance is updated.")),
                ("Source", sourceSchema),
                ("Name", RecipeStepSchemaBuilders.String().Description("Unique tool instance name. Used to derive the function name exposed to the AI model and cannot change once created.")),
                ("Description", RecipeStepSchemaBuilders.String().Description("Description the AI model uses to decide when to call the instance.")),
                ("CreatedUtc", RecipeStepSchemaBuilders.String().Description("Optional creation timestamp to preserve during import.")),
                ("OwnerId", RecipeStepSchemaBuilders.String().Description("Optional owner user identifier.")),
                ("Author", RecipeStepSchemaBuilders.String().Description("Optional author name recorded with the instance.")),
                ("Properties", propertiesSchema))
            .Required("Name")
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            "AIToolInstances",
            [("instances", RecipeStepSchemaBuilders.Array(instanceSchema, 1).Description("The AI tool instances to create or update."))],
            ["instances"]);
    }

    private static JsonSchemaBuilder BuildParametersMetadataSchema()
    {
        var parameterSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", RecipeStepSchemaBuilders.String().Description("Parameter name. For model-filled parameters this is the property name in the function schema and must be a valid identifier, unique within the instance.")),
                ("Description", RecipeStepSchemaBuilders.String().Description("Natural-language description shown to the AI model. Required for model-filled parameters.")),
                ("Type", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("String", "Integer", "Number", "Boolean", "Array", "Object").Description("JSON schema type of the parameter.")),
                ("Fill", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Model", "Fixed", "Context").Description("Who supplies the value at invocation time: Model (the AI model), Fixed (a pinned value), or Context (resolved server-side from the request context).")),
                ("Required", RecipeStepSchemaBuilders.Boolean().Description("Whether the AI model must supply the value. Only meaningful when Fill is Model.")),
                ("DefaultValue", new JsonSchemaBuilder().Description("Value applied when the model omits an optional parameter, or the pinned value when Fill is Fixed. A Fixed secret value is stored encrypted at rest.")),
                ("AllowedValues", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("Closed set of accepted values, emitted as the schema enum and enforced by the binder. Empty means any value of the declared type.")),
                ("ContextKey", RecipeStepSchemaBuilders.String().Description("Well-known context key resolved when Fill is Context, for example user.id.")),
                ("Binding", RecipeStepSchemaBuilders.String().Description("Source-specific placement of the resolved value, expressed as Target or Target:name (for example Query:orderId). Valid targets are declared by the owning source.")),
                ("IsSecret", RecipeStepSchemaBuilders.Boolean().Description("Whether a Fixed value is a credential. Secret values are stored encrypted at rest and never returned to the UI in clear text.")))
            .Required("Name")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Parameters", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(parameterSchema)
                    .Description("The declared parameters, in the order they appear in the function schema the AI model sees.")))
            .AdditionalProperties(true);
    }

    private static JsonSchemaBuilder BuildHttpApiRequestSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("BaseUrl", RecipeStepSchemaBuilders.String().Description("Absolute HTTP or HTTPS URL the request targets.")),
                ("HttpMethod", RecipeStepSchemaBuilders.String().Description("HTTP method: GET, POST, PUT, PATCH, or DELETE.")),
                ("AuthenticationType", RecipeStepSchemaBuilders.String().Description("Authentication type: None, ApiKey, Basic, or OAuth2.")),
                ("ApiKeyHeaderName", RecipeStepSchemaBuilders.String().Description("Header name used to send the API key when AuthenticationType is ApiKey.")),
                ("ApiKey", RecipeStepSchemaBuilders.String().Description("API key credential. Stored encrypted at rest.")),
                ("BearerToken", RecipeStepSchemaBuilders.String().Description("Bearer token credential. Stored encrypted at rest.")),
                ("Username", RecipeStepSchemaBuilders.String().Description("Basic authentication user name.")),
                ("Password", RecipeStepSchemaBuilders.String().Description("Basic authentication password. Stored encrypted at rest.")),
                ("TokenEndpoint", RecipeStepSchemaBuilders.String().Description("OAuth 2.0 token endpoint.")),
                ("ClientId", RecipeStepSchemaBuilders.String().Description("OAuth 2.0 client identifier.")),
                ("ClientSecret", RecipeStepSchemaBuilders.String().Description("OAuth 2.0 client secret. Stored encrypted at rest.")),
                ("Scope", RecipeStepSchemaBuilders.String().Description("OAuth 2.0 scope.")),
                ("DefaultHeaders", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(RecipeStepSchemaBuilders.String())
                    .Description("Static headers always added to the request.")),
                ("AllowModelProvidedPath", RecipeStepSchemaBuilders.Boolean().Description("Whether the AI model may supply a relative path.")),
                ("AllowModelProvidedQuery", RecipeStepSchemaBuilders.Boolean().Description("Whether the AI model may supply query string parameters.")),
                ("AllowModelProvidedBody", RecipeStepSchemaBuilders.Boolean().Description("Whether the AI model may supply a request body.")),
                ("TimeoutSeconds", RecipeStepSchemaBuilders.Integer().Description("Per-request timeout in seconds. Leave empty to use the default.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildSitemapSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("BaseUrl", RecipeStepSchemaBuilders.String().Description("Root URL of the documentation site.")),
                ("SitemapUrl", RecipeStepSchemaBuilders.String().Description("Explicit sitemap URL. Leave empty to derive it from the base URL.")),
                ("MaxResults", RecipeStepSchemaBuilders.Integer().Description("Maximum passages returned for a single search.")),
                ("MaxPages", RecipeStepSchemaBuilders.Integer().Description("Maximum pages the crawler indexes for this site.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildSearchIndexSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("BaseUrl", RecipeStepSchemaBuilders.String().Description("Root URL of the documentation site.")),
                ("IndexUrl", RecipeStepSchemaBuilders.String().Description("Explicit prebuilt search index URL. Leave empty to derive it from the base URL.")),
                ("MaxResults", RecipeStepSchemaBuilders.Integer().Description("Maximum passages returned for a single search.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAlgoliaSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ApplicationId", RecipeStepSchemaBuilders.String().Description("Algolia application identifier.")),
                ("ApiKey", RecipeStepSchemaBuilders.String().Description("Algolia search-only API key. Stored encrypted at rest.")),
                ("IndexName", RecipeStepSchemaBuilders.String().Description("Algolia index name.")),
                ("MaxResults", RecipeStepSchemaBuilders.Integer().Description("Maximum passages returned for a single search.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildWebsiteSearchSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("BaseUrl", RecipeStepSchemaBuilders.String().Description("Root URL of the site to search (for example https://www.example.com).")),
                ("SearchPath", RecipeStepSchemaBuilders.String().Description("Search endpoint path appended to the base URL. Defaults to the WordPress REST search endpoint.")),
                ("QueryParameter", RecipeStepSchemaBuilders.String().Description("Query-string parameter that carries the model's free-text query. Defaults to 'search'.")),
                ("ExtraQuery", RecipeStepSchemaBuilders.String().Description("Fixed extra query-string parameters always appended to the request. Defaults to '_embed=1'.")),
                ("ResultsPath", RecipeStepSchemaBuilders.String().Description("Dotted path to the results array in the response. Empty means the response body is itself the array.")),
                ("TitlePath", RecipeStepSchemaBuilders.String().Description("Dotted path, relative to each result, to the result title. Defaults to 'title'.")),
                ("UrlPath", RecipeStepSchemaBuilders.String().Description("Dotted path, relative to each result, to the result URL. Defaults to 'url'.")),
                ("SnippetPath", RecipeStepSchemaBuilders.String().Description("Dotted path, relative to each result, to the text snippet. Defaults to the embedded WordPress excerpt.")),
                ("MaxResults", RecipeStepSchemaBuilders.Integer().Description("Maximum results returned for a single search.")))
            .AdditionalProperties(true);
}
