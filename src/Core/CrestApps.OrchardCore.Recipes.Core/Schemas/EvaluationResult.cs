namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Captures the result of validating a JSON document against a schema.
/// </summary>
public sealed class EvaluationResult
{
    internal EvaluationResult(bool valid) => IsValid = valid;

    /// <summary>
    /// Whether the JSON document satisfies every constraint in the schema.
    /// </summary>
    public bool IsValid { get; }
}
