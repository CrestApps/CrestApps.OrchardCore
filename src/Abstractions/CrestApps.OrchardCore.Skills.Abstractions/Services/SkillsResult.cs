namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// What a skills or certifications change did: whether it was saved, or why not.
/// </summary>
public sealed class SkillsResult
{
    private readonly List<string> _errors = [];

    /// <summary>
    /// Gets why the change was refused; empty when it was saved.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Gets whether the change was saved.
    /// </summary>
    public bool Succeeded => _errors.Count == 0;

    /// <summary>
    /// Records a reason the change is refused.
    /// </summary>
    /// <param name="error">The reason.</param>
    public void Fail(string error)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);

        _errors.Add(error);
    }
}
