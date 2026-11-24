namespace CrestApps.OrchardCore.Learning;

/// <summary>
/// The registered course item kinds.
/// </summary>
public sealed class CourseItemKindOptions
{
    private readonly Dictionary<string, CourseItemKindDescriptor> _kinds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered kinds.
    /// </summary>
    public IEnumerable<CourseItemKindDescriptor> Kinds => _kinds.Values;

    /// <summary>
    /// Adds a kind, or returns the one already registered under its name.
    /// </summary>
    /// <param name="name">The name of the kind.</param>
    /// <returns>The descriptor to configure.</returns>
    public CourseItemKindDescriptor GetOrAdd(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!_kinds.TryGetValue(name, out var descriptor))
        {
            descriptor = new CourseItemKindDescriptor(name);
            _kinds[name] = descriptor;
        }

        return descriptor;
    }

    /// <summary>
    /// Finds a kind by name.
    /// </summary>
    /// <param name="name">The name of the kind.</param>
    /// <returns>The descriptor, or <see langword="null"/>.</returns>
    public CourseItemKindDescriptor Find(string name)
        => !string.IsNullOrEmpty(name) && _kinds.TryGetValue(name, out var descriptor) ? descriptor : null;
}
