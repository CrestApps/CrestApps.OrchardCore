using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.UrlRewriting.Sources;

/// <summary>
/// Describes the recipe schema for the <c>Rewrite</c> rewrite rule source.
/// </summary>
public sealed class UrlRewriteRuleSourceSchema : RewriteRuleSourceSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "Rewrite";

    /// <inheritdoc />
    protected override string DisplayText => "Rewrite";

    /// <inheritdoc />
    protected override string Description => "Rewrites the incoming request to a new URL server-side without changing the address shown to the client.";

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Pattern", "SubstitutionPattern"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RewriteRuleSourceSchemaContext context)
    {
        yield return ("Pattern", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The regular expression matched against the incoming request path, for example ^article/(.*)$."));

        yield return ("SubstitutionPattern", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The replacement URL the matched request is rewritten to. It can reference capture groups from the match pattern, such as /blog/$1."));

        yield return ("IsCaseInsensitive", new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description("Whether the match pattern ignores case. Adds the 'NC' Apache mod_rewrite flag when true."));

        yield return ("QueryStringPolicy", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum("Append", "Drop")
            .Description("How the original request query string is handled. 'Append' keeps it (QSA flag) and 'Drop' removes it (QSD flag)."));

        yield return ("SkipFurtherRules", new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description("Whether the remaining rules are skipped once this rule matches. Adds the 'L' Apache mod_rewrite flag when true."));
    }
}
