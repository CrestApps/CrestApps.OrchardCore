using CrestApps.Core;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Reads and writes the conversation variables kept on an AI session, or on an activity before its session exists.
/// </summary>
public static class AIConversationVariablesExtensions
{
    /// <summary>
    /// Gets the session's conversation variables.
    /// </summary>
    /// <param name="session">The AI session, or the activity the conversation belongs to.</param>
    /// <returns>The variables by name; empty when none were set.</returns>
    public static IReadOnlyDictionary<string, string> GetConversationVariables(this ExtensibleEntity session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.TryGet<AIConversationVariablesMetadata>(out var metadata) && metadata.Values is not null
            ? new Dictionary<string, string>(metadata.Values, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sets conversation variables on the session, keeping the ones not named. A <see langword="null"/> value
    /// removes the variable.
    /// </summary>
    /// <param name="session">The AI session, or the activity the conversation belongs to.</param>
    /// <param name="values">The variables to set.</param>
    /// <returns><see langword="true"/> when a variable changed; otherwise <see langword="false"/>.</returns>
    public static bool SetConversationVariables(this ExtensibleEntity session, IEnumerable<KeyValuePair<string, string>> values)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (values is null)
        {
            return false;
        }

        var changed = false;

        session.Alter<AIConversationVariablesMetadata>(metadata =>
        {
            metadata.Values ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, value) in values)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var key = name.Trim();

                if (value is null)
                {
                    changed |= metadata.Values.Remove(key);

                    continue;
                }

                if (!metadata.Values.TryGetValue(key, out var existing) || !string.Equals(existing, value, StringComparison.Ordinal))
                {
                    metadata.Values[key] = value;
                    changed = true;
                }
            }
        });

        return changed;
    }

    /// <summary>
    /// Sets one conversation variable on the session.
    /// </summary>
    /// <param name="session">The AI session, or the activity the conversation belongs to.</param>
    /// <param name="name">The variable's name.</param>
    /// <param name="value">The value, or <see langword="null"/> to remove it.</param>
    /// <returns><see langword="true"/> when the variable changed; otherwise <see langword="false"/>.</returns>
    public static bool SetConversationVariable(this ExtensibleEntity session, string name, string value)
        => session.SetConversationVariables([new KeyValuePair<string, string>(name, value)]);
}
