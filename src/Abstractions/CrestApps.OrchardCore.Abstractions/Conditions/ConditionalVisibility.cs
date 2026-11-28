namespace CrestApps.OrchardCore.Conditions;

/// <summary>
/// Works out which of a set of items (questions, fields, steps) show, when each may have a rule that reads the others.
/// An item shows when its rule is met; a condition that reads a hidden item is not met, so a chain of questions closes
/// when an early answer changes.
/// </summary>
public static class ConditionalVisibility
{
    /// <summary>
    /// Gets the items that are hidden.
    /// </summary>
    /// <param name="rules">The rule of each item that has one, by the item's key (the key conditions use as source).</param>
    /// <param name="lookup">
    /// Reads a source, an item or anything else, as <see cref="ConditionEvaluator.IsMet(ConditionRule, Func{string, ConditionValue})"/>
    /// expects: <see langword="null"/> for an unknown source.
    /// </param>
    /// <param name="comparer">How item keys compare; ordinal by default.</param>
    /// <returns>The keys of the hidden items.</returns>
    /// <remarks>
    /// Items caught in a cycle of rules show (editors refuse cycles; see <see cref="FindCycle"/>), so a broken definition
    /// never hides an item.
    /// </remarks>
    public static IReadOnlySet<string> GetHidden(
        IReadOnlyDictionary<string, ConditionRule> rules,
        Func<string, ConditionValue> lookup,
        IEqualityComparer<string> comparer = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(lookup);

        comparer ??= StringComparer.Ordinal;

        var shown = new Dictionary<string, bool>(comparer);
        var visiting = new HashSet<string>(comparer);
        var hidden = new HashSet<string>(comparer);
        var cyclic = FindCyclic(rules, comparer);

        foreach (var key in rules.Keys)
        {
            if (!Shows(key))
            {
                hidden.Add(key);
            }
        }

        return hidden;

        bool Shows(string key)
        {
            if (shown.TryGetValue(key, out var known))
            {
                return known;
            }

            // An item caught in a cycle shows, as one without a rule would.
            if (!rules.TryGetValue(key, out var rule) || ConditionRule.IsEmpty(rule) || cyclic.Contains(key) || !visiting.Add(key))
            {
                return true;
            }

            var result = ConditionEvaluator.IsMet(rule, source =>
            {
                var value = lookup(source);

                if (value is null)
                {
                    return null;
                }

                // A hidden item answers nothing: the condition reading it fails, whatever its operator.
                return rules.ContainsKey(source) && !Shows(source) ? ConditionValue.Withheld : value;
            });

            visiting.Remove(key);
            shown[key] = result;

            return result;
        }
    }

    /// <summary>
    /// Finds a cycle among the rules, such as A showing when B and B showing when A.
    /// </summary>
    /// <param name="rules">The rule of each item, by its key.</param>
    /// <param name="comparer">How item keys compare; ordinal by default.</param>
    /// <returns>The keys in a cycle, in order, or an empty list when there is none.</returns>
    public static IReadOnlyList<string> FindCycle(IReadOnlyDictionary<string, ConditionRule> rules, IEqualityComparer<string> comparer = null)
    {
        ArgumentNullException.ThrowIfNull(rules);

        comparer ??= StringComparer.Ordinal;

        var done = new HashSet<string>(comparer);
        var path = new List<string>();
        var onPath = new HashSet<string>(comparer);

        foreach (var key in rules.Keys)
        {
            if (Visit(key) is { Count: > 0 } cycle)
            {
                return cycle;
            }
        }

        return [];

        List<string> Visit(string key)
        {
            if (done.Contains(key))
            {
                return null;
            }

            if (!onPath.Add(key))
            {
                var start = path.FindIndex(item => comparer.Equals(item, key));

                return path[start..];
            }

            path.Add(key);

            foreach (var source in Sources(rules.GetValueOrDefault(key)))
            {
                if (rules.ContainsKey(source) && Visit(source) is { Count: > 0 } cycle)
                {
                    return cycle;
                }
            }

            path.RemoveAt(path.Count - 1);
            onPath.Remove(key);
            done.Add(key);

            return null;
        }
    }

    // The items that can reach themselves through the sources of their rules.
    private static HashSet<string> FindCyclic(IReadOnlyDictionary<string, ConditionRule> rules, IEqualityComparer<string> comparer)
    {
        var cyclic = new HashSet<string>(comparer);

        foreach (var start in rules.Keys)
        {
            var seen = new HashSet<string>(comparer);
            var pending = new Stack<string>(Sources(rules[start]).Where(rules.ContainsKey));

            while (pending.Count > 0)
            {
                var current = pending.Pop();

                if (comparer.Equals(current, start))
                {
                    cyclic.Add(start);

                    break;
                }

                if (!seen.Add(current))
                {
                    continue;
                }

                foreach (var next in Sources(rules[current]).Where(rules.ContainsKey))
                {
                    pending.Push(next);
                }
            }
        }

        return cyclic;
    }

    /// <summary>
    /// Gets the sources a rule reads, in its conditions and in its nested groups.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The distinct sources, in order.</returns>
    public static IEnumerable<string> Sources(ConditionRule rule)
        => AllSources(rule).Distinct(StringComparer.Ordinal);

    private static IEnumerable<string> AllSources(ConditionRule rule)
    {
        if (rule is null)
        {
            yield break;
        }

        foreach (var condition in rule.Conditions ?? [])
        {
            if (!string.IsNullOrWhiteSpace(condition?.Source))
            {
                yield return condition.Source;
            }
        }

        foreach (var group in rule.Groups ?? [])
        {
            foreach (var source in AllSources(group))
            {
                yield return source;
            }
        }
    }
}
