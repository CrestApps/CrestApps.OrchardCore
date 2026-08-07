using CrestApps.OrchardCore.Recipes.Core.Schemas.UrlRewriting;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Builds JSON schemas describing the rewrite rule sources available on the current tenant, used by the
/// <c>UrlRewriting</c> recipe step to describe each entry of its <c>Rules</c> array.
/// </summary>
public interface IRewriteRuleSchemaService
{
    /// <summary>
    /// Gets a descriptor for every rewrite rule source contributed through an
    /// <see cref="IRewriteRuleSourceSchemaDefinition"/>.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<IReadOnlyList<RewriteRuleSourceDescriptor>> GetSourceDescriptorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the schema describing a single entry of the <c>Rules</c> array, keyed on the <c>Source</c>
    /// discriminator.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetRuleSchemaAsync(CancellationToken cancellationToken = default);
}
