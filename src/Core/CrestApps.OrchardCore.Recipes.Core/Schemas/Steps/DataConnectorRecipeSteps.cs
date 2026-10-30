using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "DataConnection" recipe step — creates or updates the connections of the Data Connectors feature.
/// </summary>
public sealed class DataConnectionRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "DataConnection";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var connectionSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", CatalogRecipeStepSchemas.ItemId("connection")),
                ("Name", RecipeStepSchemaBuilders.String().Pattern("^[A-Za-z][A-Za-z0-9_]{0,127}$").Description("Technical name of the connection: a letter, then letters, digits and underscores. A connection without a matching ItemId is matched by this name. Made from DisplayText when omitted.")),
                ("DisplayText", RecipeStepSchemaBuilders.String().Description("Name of the connection shown in the admin UI.")),
                ("Description", RecipeStepSchemaBuilders.String().Description("Optional description of the connection.")),
                ("Connector", RecipeStepSchemaBuilders.SuggestedString(DataConnectorRecipeSchemas.ConnectorNames).Description("The connector the connection uses. Its feature must be enabled. An existing connection's connector can't change.")),
                ("Properties", RecipeStepSchemaBuilders.Object().Description("The connector's settings, keyed by the name of their type, as a deployment plan exports them (for example HttpConnectionSettings, SqlServerConnectionSettings or MediaFilesConnectionSettings). Members not given are kept.")),
                ("Secrets", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(RecipeStepSchemaBuilders.String())
                    .Description("The connection's secrets in clear text, keyed by name (for example Password, ApiKey, BearerToken or ClientSecret). They are encrypted when stored. A deployment plan never exports them; an empty or missing secret keeps the one already stored.")),
                ("CreatedUtc", RecipeStepSchemaBuilders.String().Description("Optional creation timestamp to preserve when importing exported records.")),
                ("ModifiedUtc", RecipeStepSchemaBuilders.String().Description("Optional last-modified timestamp to preserve when importing exported records.")),
                ("Author", RecipeStepSchemaBuilders.String().Description("Optional author name recorded with the connection.")))
            .AnyOf(
                new JsonSchemaBuilder().Required("Name"),
                new JsonSchemaBuilder().Required("DisplayText"))
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            "DataConnection",
            [("Connections", RecipeStepSchemaBuilders.Array(connectionSchema, 1).Description("The data connections to create or update."))],
            ["Connections"]);
    }
}

/// <summary>
/// Schema for the "ConnectedDataSet" recipe step — creates or updates the connected data sets of the Data Connectors feature.
/// </summary>
public sealed class ConnectedDataSetRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "ConnectedDataSet";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var fieldSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", RecipeStepSchemaBuilders.String().Pattern("^[A-Za-z][A-Za-z0-9_]{0,127}$").Description("Name reports and pipelines read the field by. Made from SourceName when omitted.")),
                ("SourceName", RecipeStepSchemaBuilders.String().Description("Name of the column, property or member at the source.")),
                ("DisplayName", RecipeStepSchemaBuilders.String().Description("Label of the field. Defaults to Name.")),
                ("Description", RecipeStepSchemaBuilders.String()),
                ("Type", RecipeStepSchemaBuilders.String().Enum("Text", "Integer", "Decimal", "Boolean", "Date", "DateTime").Description("Type the field is read as.")),
                ("SourceType", RecipeStepSchemaBuilders.String().Enum("Text", "Integer", "Decimal", "Boolean", "Date", "DateTime").Description("Type the source described.")),
                ("IsIdentifier", RecipeStepSchemaBuilders.Boolean().Description("Whether the field identifies a row. An incremental refresh needs at least one.")),
                ("IsIncluded", RecipeStepSchemaBuilders.Boolean().Description("Whether reports and pipelines see the field. Defaults to true.")))
            .AnyOf(
                new JsonSchemaBuilder().Required("SourceName"),
                new JsonSchemaBuilder().Required("Name"))
            .AdditionalProperties(true);

        var refreshSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Mode", RecipeStepSchemaBuilders.String().Enum("Full", "Incremental").Description("Whether a refresh reads every row, or only the rows changed since the last one.")),
                ("IncrementalField", RecipeStepSchemaBuilders.String().Description("For an incremental refresh: the date or number field that grows when a row changes.")),
                ("IntervalMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Minimum(1).Maximum(525600).Description("Minutes between scheduled refreshes. Null refreshes only on demand.")),
                ("SchemaChangePolicy", RecipeStepSchemaBuilders.String().Enum("Accept", "Fail").Description("Whether a refresh accepts a change of the source's fields, or fails and keeps the rows it has.")))
            .AdditionalProperties(true);

        var dataSetSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", CatalogRecipeStepSchemas.ItemId("data set")),
                ("Name", RecipeStepSchemaBuilders.String().Pattern("^[A-Za-z][A-Za-z0-9_]{0,127}$").Description("Technical name of the data set, which reports, pipelines and its read permission store. A data set without a matching ItemId is matched by this name. It can't change once created. Made from DisplayText when omitted.")),
                ("DisplayText", RecipeStepSchemaBuilders.String().Description("Name of the data set shown in the admin UI and in data source pickers.")),
                ("Description", RecipeStepSchemaBuilders.String().Description("Optional description of the data set.")),
                ("ConnectionId", RecipeStepSchemaBuilders.String().Description("Identifier of the connection the data set reads.")),
                ("ConnectionName", RecipeStepSchemaBuilders.String().Description("Technical name of the connection the data set reads, used when no connection has ConnectionId.")),
                ("Resource", RecipeStepSchemaBuilders.String().Description("What the data set reads at the connection: a table or view, an index, an API path, or a media file.")),
                ("AccessMode", RecipeStepSchemaBuilders.String().Enum("Live", "Imported").Description("Live reads the source on every read; Imported copies the rows into the site on a schedule. Defaults to Live when the connector can read live.")),
                ("IsEnabled", RecipeStepSchemaBuilders.Boolean().Description("Whether the data set is offered to reports and pipelines and refreshed. Defaults to true.")),
                ("Fields", RecipeStepSchemaBuilders.Array(fieldSchema).Description("The fields of the data set. When omitted on a new data set, they are read from the source if it can be reached, and otherwise at the first refresh.")),
                ("DefaultDateField", RecipeStepSchemaBuilders.String().Description("Name of an included date field that reports filter by default.")),
                ("Refresh", refreshSchema.Description("How an imported data set is refreshed.")),
                ("Properties", RecipeStepSchemaBuilders.Object().Description("The connector's settings of the data set, keyed by the name of their type (for example RelationalDataSetSettings, HttpApiDataSetSettings, ElasticsearchDataSetSettings or MediaFileDataSetSettings). Members not given are kept.")),
                ("CreatedUtc", RecipeStepSchemaBuilders.String().Description("Optional creation timestamp to preserve when importing exported records.")),
                ("ModifiedUtc", RecipeStepSchemaBuilders.String().Description("Optional last-modified timestamp to preserve when importing exported records.")),
                ("Author", RecipeStepSchemaBuilders.String().Description("Optional author name recorded with the data set.")))
            .AnyOf(
                new JsonSchemaBuilder().Required("Name"),
                new JsonSchemaBuilder().Required("DisplayText"))
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            "ConnectedDataSet",
            [("DataSets", RecipeStepSchemaBuilders.Array(dataSetSchema, 1).Description("The connected data sets to create or update. Imported data sets that hold no rows yet are queued for their first import."))],
            ["DataSets"]);
    }
}

internal static class DataConnectorRecipeSchemas
{
    /// <summary>
    /// The connectors CrestApps ships. Others can be registered, so they are suggestions, not a closed list.
    /// </summary>
    public static readonly string[] ConnectorNames = ["Elasticsearch", "HttpApi", "MediaFiles", "SqlServer"];
}
