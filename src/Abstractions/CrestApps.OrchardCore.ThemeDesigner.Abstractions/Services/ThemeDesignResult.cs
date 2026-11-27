namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// The outcome of a change to a theme draft.
/// </summary>
public sealed class ThemeDesignResult
{
    private static readonly ThemeDesignResult _success = new([]);

    private ThemeDesignResult(IReadOnlyList<string> errors)
    {
        Errors = errors;
    }

    /// <summary>
    /// Gets whether the change was made.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;

    /// <summary>
    /// Gets why the change was not made.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Gets a result for a change that was made.
    /// </summary>
    public static ThemeDesignResult Success()
        => _success;

    /// <summary>
    /// Gets a result for a change that was not made.
    /// </summary>
    /// <param name="errors">Why the change was not made.</param>
    public static ThemeDesignResult Failed(params IEnumerable<string> errors)
    {
        var list = errors?.Where(error => !string.IsNullOrWhiteSpace(error)).ToArray() ?? [];

        return list.Length == 0
            ? new ThemeDesignResult(["The change could not be made."])
            : new ThemeDesignResult(list);
    }
}
