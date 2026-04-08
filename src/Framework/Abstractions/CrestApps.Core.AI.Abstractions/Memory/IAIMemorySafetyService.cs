namespace CrestApps.Core.AI.Memory;

/// <summary>
/// Validates AI memory entries for safety, ensuring that names, descriptions,
/// and content do not contain harmful, disallowed, or policy-violating material
/// before they are persisted.
/// </summary>
public interface IAIMemorySafetyService
{
    /// <summary>
    /// Validates the specified memory entry fields for safety and policy compliance.
    /// </summary>
    /// <param name="name">The name of the memory entry to validate.</param>
    /// <param name="description">The description of the memory entry to validate.</param>
    /// <param name="content">The content of the memory entry to validate.</param>
    /// <param name="errorMessage">When validation fails, contains the error message describing the violation.</param>
    /// <returns><see langword="true"/> if the entry passes safety validation; otherwise, <see langword="false"/>.</returns>
    bool TryValidate(string name, string description, string content, out string errorMessage);
}
