using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.UrlRewriting.Sources;

/// <summary>
/// Describes the recipe schema for the <c>Redirect</c> rewrite rule source.
/// </summary>
public sealed class UrlRedirectRuleSourceSchema : RewriteRuleSourceSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "Redirect";

    /// <inheritdoc />
    protected override string DisplayText => "Redirect";

    /// <inheritdoc />
    protected override string Description => "Redirects the incoming request to a new URL and returns the configured redirect status code.";

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
            .Description("The replacement URL the matched request is redirected to. It can reference capture groups from the match pattern, such as /blog/$1."));

        yield return ("IsCaseInsensitive", new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description("Whether the match pattern ignores case. Adds the 'NC' Apache mod_rewrite flag when true."));

        yield return ("QueryStringPolicy", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum("Append", "Drop")
            .Description("How the original request query string is handled. 'Append' keeps it (QSA flag) and 'Drop' removes it (QSD flag)."));

        yield return ("RedirectType", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum("Found", "MovedPermanently", "TemporaryRedirect", "PermanentRedirect")
            .Description("The HTTP redirect status code returned. 'Found' is 302, 'MovedPermanently' is 301, 'TemporaryRedirect' is 307 and 'PermanentRedirect' is 308."));
    }
}
