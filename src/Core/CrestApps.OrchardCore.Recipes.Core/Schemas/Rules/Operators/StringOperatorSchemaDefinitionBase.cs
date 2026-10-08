using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Operators;

/// <summary>
/// Provides the shared schema surface for the string comparison operators of the Rules module. Every string
/// operator exposes a <c>CaseSensitive</c> flag.
/// </summary>
public abstract class StringOperatorSchemaDefinitionBase : RuleConditionOperatorSchemaDefinitionBase
{
    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("CaseSensitive", RuleConditionSchemaBuilders.Boolean("Whether the comparison is case sensitive. Defaults to false."));
    }
}
