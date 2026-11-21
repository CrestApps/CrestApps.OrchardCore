using System.Globalization;
using System.Text;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The rules of a public suffix list, applied as the list's algorithm says (https://publicsuffix.org/list/): of the
/// rules a name matches, an exception rule (<c>!www.ck</c>) prevails, and its public suffix is the rule without its
/// first label; otherwise the rule with the most labels prevails, a wildcard rule (<c>*.ck</c>) matching any one label
/// in place of its <c>*</c>; and a name no rule matches falls under its top-level domain.
/// </summary>
/// <remarks>
/// Rules and names are compared in lower case and Punycode, so an internationalized name matches in either form.
/// </remarks>
internal sealed class PublicSuffixRules
{
    // Each set is keyed on the rule as a name: co.uk; ck for *.ck; www.ck for !www.ck.
    private readonly HashSet<string> _rules = new(StringComparer.Ordinal);
    private readonly HashSet<string> _wildcards = new(StringComparer.Ordinal);
    private readonly HashSet<string> _exceptions = new(StringComparer.Ordinal);

    /// <summary>
    /// How many rules there are.
    /// </summary>
    public int Count
        => _rules.Count + _wildcards.Count + _exceptions.Count;

    /// <summary>
    /// Adds a rule in the list's format: <c>co.uk</c>, <c>*.ck</c> or <c>!www.ck</c>.
    /// </summary>
    /// <param name="rule">The rule.</param>
    public void Add(string rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            return;
        }

        if (rule.StartsWith('!'))
        {
            AddName(_exceptions, rule[1..]);
        }
        else if (rule.StartsWith("*.", StringComparison.Ordinal))
        {
            AddName(_wildcards, rule[2..]);
        }
        else
        {
            AddName(_rules, rule);
        }
    }

    /// <summary>
    /// Whether <paramref name="domain"/> is itself a public suffix.
    /// </summary>
    /// <param name="domain">The domain, without a trailing dot.</param>
    public bool IsPublicSuffix(string domain)
    {
        var name = Normalize(domain);

        return name is not null && GetPublicSuffixOf(name).Length == name.Length;
    }

    /// <summary>
    /// The registrable domain of <paramref name="domain"/>, its public suffix with the one label before it, in lower case
    /// and Punycode; or <see langword="null"/> when the name is a public suffix or not a valid name.
    /// </summary>
    /// <param name="domain">The domain, without a trailing dot.</param>
    public string GetRegistrableDomain(string domain)
    {
        var name = Normalize(domain);

        if (name is null)
        {
            return null;
        }

        var suffix = GetPublicSuffixOf(name);

        if (suffix.Length == name.Length)
        {
            return null;
        }

        // The dot before the suffix, then the one before the label ahead of it.
        var dot = name.Length - suffix.Length - 1;
        var start = name.LastIndexOf('.', dot - 1) + 1;

        return name[start..];
    }

    // The public suffix of a normalized name: the longest of its suffixes a rule says is one.
    private ReadOnlySpan<char> GetPublicSuffixOf(string name)
    {
        // Where each of the name's suffixes starts, the whole name first. A name is at most 253 characters, so 127 labels.
        Span<int> starts = stackalloc int[128];
        var count = 0;
        starts[count++] = 0;

        for (var index = 0; index < name.Length && count < starts.Length; index++)
        {
            if (name[index] == '.')
            {
                starts[count++] = index + 1;
            }
        }

        var exceptions = _exceptions.GetAlternateLookup<ReadOnlySpan<char>>();
        var rules = _rules.GetAlternateLookup<ReadOnlySpan<char>>();
        var wildcards = _wildcards.GetAlternateLookup<ReadOnlySpan<char>>();

        // An exception rule prevails over every other: the suffix is the rule without its first label.
        for (var i = 0; i < count - 1; i++)
        {
            if (exceptions.Contains(name.AsSpan(starts[i])))
            {
                return name.AsSpan(starts[i + 1]);
            }
        }

        // Then the rule with the most labels: at each length, the exact rule or the wildcard one over the rest.
        for (var i = 0; i < count; i++)
        {
            if (rules.Contains(name.AsSpan(starts[i])) ||
                (i + 1 < count && wildcards.Contains(name.AsSpan(starts[i + 1]))))
            {
                return name.AsSpan(starts[i]);
            }
        }

        // No rule matches: the implicit "*" rule makes the top-level domain the suffix.
        return name.AsSpan(starts[count - 1]);
    }

    private static void AddName(HashSet<string> set, string name)
    {
        if (Normalize(name) is { } normalized)
        {
            set.Add(normalized);
        }
    }

    // The name in lower case and Punycode, or null when it is empty, longer than a name can be, or has an empty label.
    private static string Normalize(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return null;
        }

        var name = domain.Trim().TrimEnd('.').ToLowerInvariant();

        if (name.Length == 0 || name.StartsWith('.') || name.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        if (!Ascii.IsValid(name))
        {
            try
            {
                // An IdnMapping instance is not safe to share between threads.
                name = new IdnMapping().GetAscii(name);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        return name.Length <= 253 ? name : null;
    }
}
