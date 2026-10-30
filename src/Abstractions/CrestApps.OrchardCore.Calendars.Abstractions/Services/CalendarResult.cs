namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// The outcome of a calendar operation: the record when it succeeded, or why it didn't.
/// </summary>
/// <typeparam name="T">The record's type.</typeparam>
public sealed class CalendarResult<T>
    where T : class
{
    private CalendarResult(T value, IReadOnlyList<CalendarError> errors)
    {
        Value = value;
        Errors = errors;
    }

    /// <summary>
    /// Gets the record the operation created or changed.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Gets why the operation failed, empty when it succeeded.
    /// </summary>
    public IReadOnlyList<CalendarError> Errors { get; }

    /// <summary>
    /// Gets whether the operation succeeded.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="value">The record.</param>
    /// <returns>The result.</returns>
    public static CalendarResult<T> Success(T value)
    {
        return new CalendarResult<T>(value, []);
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="value">The record that was refused.</param>
    /// <param name="errors">Why it was refused.</param>
    /// <returns>The result.</returns>
    public static CalendarResult<T> Failed(T value, IEnumerable<CalendarError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var list = errors.ToList();

        if (list.Count == 0)
        {
            throw new ArgumentException("A failed result needs at least one error.", nameof(errors));
        }

        return new CalendarResult<T>(value, list);
    }
}
