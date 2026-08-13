using CrestApps.OrchardCore.Recipes.Core.Schemas.UrlRewriting;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces the JSON schema and metadata that describe a single rewrite rule source inside the <c>Rules</c>
/// array of the <c>UrlRewriting</c> recipe step.
/// </summary>
/// <remarks>
/// Implement this interface when a module contributes a custom rewrite rule source and wants the generated
/// recipe schema to describe the source's members. Registering the implementation as
/// <see cref="IRewriteRuleSourceSchemaDefinition"/> is enough for the <c>UrlRewriting</c> recipe step to pick
/// it up. Prefer deriving from
/// <see cref="CrestApps.OrchardCore.Recipes.Core.Schemas.UrlRewriting.RewriteRuleSourceSchemaDefinitionBase"/>,
/// which handles the standard rule envelope.
/// </remarks>
public interface IRewriteRuleSourceSchemaDefinition
{
    /// <summary>
    /// Gets the rewrite rule source name that this definition describes, matching the <c>Source</c>
    /// discriminator, for example <c>Redirect</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the schema and metadata describing the rewrite rule source.
    /// </summary>
    /// <param name="context">The context describing the source being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<RewriteRuleSourceSchema> GetSourceSchemaAsync(RewriteRuleSourceSchemaContext context, CancellationToken cancellationToken = default);
}
