using System.Globalization;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// One rule in a rule set: which rule, how it is enforced and its parameters.
/// </summary>
public sealed class LaborRuleInstance
{
    /// <summary>
    /// Gets or sets the rule's id within the set.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the key of the rule in the catalog.
    /// </summary>
    public string RuleKey { get; set; }

    /// <summary>
    /// Gets or sets what happens when it is broken.
    /// </summary>
    public LaborRuleEnforcement Enforcement { get; set; } = LaborRuleEnforcement.Warn;

    /// <summary>
    /// Gets or sets the parameters, by name, written invariantly.
    /// </summary>
    public IDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Returns a number parameter, or a fallback when it is missing or not a number.
    /// </summary>
    /// <param name="name">The parameter.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The number.</returns>
    public decimal Number(string name, decimal fallback)
    {
        return Parameters.TryGetValue(name, out var value) && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number
            : fallback;
    }

    /// <summary>
    /// Returns a time-of-day parameter, or a fallback when it is missing or not a time.
    /// </summary>
    /// <param name="name">The parameter.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The time.</returns>
    public TimeOnly Time(string name, TimeOnly fallback)
    {
        return Parameters.TryGetValue(name, out var value) && TimeOnly.TryParseExact(value, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : fallback;
    }
}
