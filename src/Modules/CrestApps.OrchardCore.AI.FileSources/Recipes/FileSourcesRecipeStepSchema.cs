using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.FileSources.Recipes;

/// <summary>
/// Describes the <c>FileSources</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class FileSourcesRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => FileSourcesStep.StepKey;

    protected override string EntriesName => FileSourcesStep.EntriesName;

    protected override string Description => "Creates or updates file sources, matched by ItemId, then by DisplayText within the same connector. The connector must be enabled on the site.";

    protected override string EntryDescription => "A file source: the connector it reads with, the AI data source it feeds and the connector's settings in Properties.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Source", "DisplayText"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the ingestion connector, such as 'FileSystem', 'Ftp' or 'Sftp'.")),
            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the file source.")),
            ("AIDataSourceId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The id of the AI data source the files are indexed into. Import the data sources first.")),
            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the file source runs.")),
            ("ReindexIntervalMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Description("How often the files are read again, in minutes.")),
            ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The connector's settings, such as the folder to read. Passwords and keys left out keep the stored ones.")),
        ];
}
