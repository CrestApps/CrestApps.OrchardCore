using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Describes the <c>AgentFeedSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class AgentFeedSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(AgentFeedSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Settings › Commerce › Agent feeds. Passwords, keys and bearer keys are stored protected with the tenant's data protection keys, so enter them again on another site.")
            .Properties(
                ("TargetCountries", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern("^[A-Z]{2}$")).Description("The two-letter codes of the countries the store sells to through agents; the store's own country when empty.")),
                ("Sftp", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Description("The daily push of the OpenAI product feed over SFTP.")
                    .Properties(
                        ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(false).Description("Whether the snapshot is pushed every day.")),
                        ("Host", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The SFTP server's host name.")),
                        ("Port", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(22).Description("The SFTP server's port.")),
                        ("Username", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The user name to sign in with.")),
                        ("HostKeyFingerprint", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The server's SHA-256 host key fingerprint. Required to push: the push doesn't run without it, and stops when the server shows another key.")),
                        ("RemoteFolder", new JsonSchemaBuilder().Type(SchemaValueType.String).Default("/").Description("The folder on the server the files go in.")),
                        ("Format", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("jsonl.gz", "csv.gz").Default("jsonl.gz").Description("The file format.")),
                        ("Currency", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The ISO 4217 code of the currency the feed is priced in, or none for each product's own.")))
                    .AdditionalProperties(true)),
                ("FeedApiTargets", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Description("The agent platforms the products are pushed to through their ACP feed API.")
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Id", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The target's id.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The name the store knows the platform by.")),
                            ("BaseUrl", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Uri).Description("The https base address of the platform's feed API.")),
                            ("FeedId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The id of the feed on the platform, or none to have the store create one.")),
                            ("TargetCountry", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The two-letter code of the country the feed is for.")),
                            ("Currency", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The ISO 4217 code of the currency the products are priced in.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether products are pushed to the platform.")))
                        .AdditionalProperties(true))))
            .AdditionalProperties(true);
}
