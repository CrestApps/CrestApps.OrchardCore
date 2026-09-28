using System.Diagnostics.CodeAnalysis;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The name of a routeable capability, in the one form the platform compares skills by.
/// <para>
/// A skill only means anything when a queue that requires it and an agent that has it agree that they are
/// talking about the same skill. That agreement used to be an accident: the administration form trimmed the
/// names a queue asked for, nothing trimmed the names an agent was given through a recipe or an import, and
/// the routing strategy compared them case-insensitively but not otherwise. An agent whose skill was stored
/// as <c>" Spanish"</c> was quietly unroutable to every queue that required <c>"Spanish"</c>, and nothing
/// anywhere reported a problem — the agent was simply never chosen.
/// </para>
/// </summary>
public readonly record struct SkillTag
{
    private readonly string _value;

    private SkillTag(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the skill name as it should be displayed, or <see langword="null"/> when this is the default value.
    /// </summary>
    public string Value => _value;

    /// <summary>
    /// Gets a value indicating whether this instance names a skill. A default <see cref="SkillTag"/> names none.
    /// </summary>
    public bool HasValue => _value is not null;

    /// <summary>
    /// Creates a skill tag from a name.
    /// </summary>
    /// <param name="name">The skill name. Surrounding whitespace is not part of the name.</param>
    /// <returns>The skill tag.</returns>
    /// <exception cref="ArgumentException">The name is empty or contains only whitespace.</exception>
    public static SkillTag Create(string name)
    {
        if (!TryCreate(name, out var skillTag))
        {
            throw new ArgumentException("A skill name cannot be empty.", nameof(name));
        }

        return skillTag;
    }

    /// <summary>
    /// Attempts to create a skill tag from a name.
    /// </summary>
    /// <param name="name">The skill name. Surrounding whitespace is not part of the name.</param>
    /// <param name="skillTag">The skill tag when the name is usable; otherwise the default value.</param>
    /// <returns><see langword="true"/> when the name is usable; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreate(string name, out SkillTag skillTag)
    {
        skillTag = default;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        skillTag = new SkillTag(name.Trim());

        return true;
    }

    /// <summary>
    /// Creates the distinct skill tags named by a sequence, discarding entries that name no skill.
    /// </summary>
    /// <param name="names">The skill names to read. A <see langword="null"/> sequence names no skills.</param>
    /// <returns>The distinct skill tags, in the order they were first named.</returns>
    public static IReadOnlyList<SkillTag> CreateAll(IEnumerable<string> names)
    {
        if (names is null)
        {
            return [];
        }

        var skillTags = new List<SkillTag>();
        var seen = new HashSet<SkillTag>();

        foreach (var name in names)
        {
            if (TryCreate(name, out var skillTag) && seen.Add(skillTag))
            {
                skillTags.Add(skillTag);
            }
        }

        return skillTags;
    }

    /// <summary>
    /// Creates the distinct skill names of a sequence, in the form the platform stores them.
    /// </summary>
    /// <param name="names">The skill names to read. A <see langword="null"/> sequence names no skills.</param>
    /// <returns>The distinct skill names.</returns>
    public static IList<string> NormalizeAll(IEnumerable<string> names)
    {
        return CreateAll(names)
            .Select(skillTag => skillTag.Value)
            .ToArray();
    }

    /// <summary>
    /// Determines whether this instance names the same skill as another.
    /// </summary>
    /// <param name="other">The skill tag to compare against.</param>
    /// <returns><see langword="true"/> when both name the same skill; otherwise, <see langword="false"/>.</returns>
    public bool Equals(SkillTag other)
    {
        return string.Equals(_value, other._value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a hash code that is equal for every spelling of the same skill name.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return _value is null
            ? 0
            : StringComparer.OrdinalIgnoreCase.GetHashCode(_value);
    }

    /// <summary>
    /// Returns the skill name, or an empty string when this is the default value.
    /// </summary>
    /// <returns>The skill name.</returns>
    public override string ToString() => _value ?? string.Empty;

    /// <summary>
    /// Determines whether a value names a skill.
    /// </summary>
    /// <param name="name">The value to inspect.</param>
    /// <returns><see langword="true"/> when the value names a skill; otherwise, <see langword="false"/>.</returns>
    public static bool IsSkillName([NotNullWhen(true)] string name) => !string.IsNullOrWhiteSpace(name);
}
