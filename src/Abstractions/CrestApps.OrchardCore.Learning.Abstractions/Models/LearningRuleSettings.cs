namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// The settings of one open rule on a course or course item: a release rule or a completion criterion. The name picks
/// the registered rule; the values are its settings as text, so a recipe can carry them.
/// </summary>
public sealed class LearningRuleSettings
{
    /// <summary>
    /// Gets or sets the name of the registered rule.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the settings of the rule.
    /// </summary>
    public IDictionary<string, string> Values { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads a setting.
    /// </summary>
    /// <param name="key">The name of the setting.</param>
    /// <returns>The value, or <see langword="null"/> when it is not set.</returns>
    public string Get(string key)
        => Values is not null && Values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
