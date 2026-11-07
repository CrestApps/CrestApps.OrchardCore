using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.WebCrawlers.Recipes;

/// <summary>
/// Describes the <c>WebCrawlers</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class WebCrawlersRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => WebCrawlersStep.StepKey;

    protected override string EntriesName => WebCrawlersStep.EntriesName;

    protected override string Description => "Creates or updates web crawlers, matched by ItemId, then by DisplayText within the same crawl strategy. The strategy must be registered on the site.";

    protected override string EntryDescription => "A web crawler: the crawl strategy, the Web AI data source it feeds and the strategy's settings in Properties.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Source", "DisplayText"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the crawl strategy, such as 'Sitemap'.")),
            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the crawler.")),
            ("AIDataSourceId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The id of the Web AI data source the pages are indexed into. Import the data sources first.")),
            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the crawler runs.")),
            ("ReindexIntervalMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Description("How often the site is crawled again, in minutes.")),
            ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The strategy's settings, such as the sitemap URL.")),
        ];
}
