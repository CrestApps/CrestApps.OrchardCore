namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// What a planning change did: the saved record, or why it was refused.
/// </summary>
/// <typeparam name="T">The record.</typeparam>
public sealed class PlanningResult<T>
{
    private readonly List<string> _errors = [];

    /// <summary>
    /// Gets or sets the saved record.
    /// </summary>
    public T Value { get; set; }

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
