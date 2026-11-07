using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.NLWeb.Recipes;

/// <summary>
/// Describes the <c>NLWebSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class NLWebSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(NLWebSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("What the NLWeb endpoint searches and how it answers.")
            .Properties(
                (nameof(NLWebSettings.IndexProfileNames), Strings("The names of the search index profiles the endpoint searches.")),
                (nameof(NLWebSettings.ContentTypes), Strings("The content types the results may be. Empty allows every type.")),
                (nameof(NLWebSettings.MaxResults), new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Default(10).Description("The most results one answer returns.")),
                (nameof(NLWebSettings.DefaultMode), new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(NLWebProtocol.Modes.List, NLWebProtocol.Modes.Summarize, NLWebProtocol.Modes.Generate).Default(NLWebProtocol.Modes.List).Description("The mode used when a request names none.")),
                (nameof(NLWebSettings.AllowLegacyRequests), Flag("Whether requests in the older NLWeb format are accepted.", true)),
                (nameof(NLWebSettings.AllowCrossOrigin), Flag("Whether pages on other sites may call the endpoint.", true)),
                (nameof(NLWebSettings.RewriteQueries), Flag("Whether an AI model rewrites the question before the search.", true)),
                (nameof(NLWebSettings.RankResults), Flag("Whether an AI model ranks the results.", false)),
                (nameof(NLWebSettings.AllowSummarize), Flag("Whether the summarize mode is offered.", true)),
                (nameof(NLWebSettings.AllowGenerate), Flag("Whether the generate mode is offered.", true)),
                (nameof(NLWebSettings.DeploymentName), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The name of the AI deployment used to rewrite, rank and answer.")),
                (nameof(NLWebSettings.DataSourceId), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The id of the AI data source searched instead of the index profiles.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder Strings(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description(description);

    private static JsonSchemaBuilder Flag(string description, bool defaultValue)
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(defaultValue).Description(description);
}
