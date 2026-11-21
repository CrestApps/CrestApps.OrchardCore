using System.Text;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The Public Suffix List (https://publicsuffix.org): the names under which anybody can register a name of their own,
/// from top-level domains (<c>com</c>) and country second levels (<c>co.uk</c>) to the shared hosting platforms whose
/// customers each get a name under them (<c>github.io</c>, <c>azurewebsites.net</c>). Nobody owns one of these, so a
/// sold site may not answer on it; and since a site's domain also holds the names under and above it, one purchase
/// naming one would hold every name under it.
/// </summary>
/// <remarks>
/// The whole list, ICANN and private sections, ships embedded in this assembly as <c>Data/public_suffix_list.dat</c>
/// (Mozilla Public License 2.0, see <c>THIRD-PARTY-NOTICES.md</c>), and is read once, on first use. Its wildcard
/// (<c>*.ck</c>) and exception (<c>!www.ck</c>) rules apply as the list's algorithm says. Refresh the copy with
/// <c>node utilities/public-suffix-list/update-public-suffix-list.mjs</c>.
/// </remarks>
internal static class PublicSuffixList
{
    internal const string ResourceName = "CrestApps.OrchardCore.Subscriptions.Core.public_suffix_list.dat";

    private static readonly Lazy<PublicSuffixRules> _rules = new(Load);

    /// <summary>
    /// How many rules the embedded list holds.
    /// </summary>
    internal static int RuleCount
        => _rules.Value.Count;

    /// <summary>
    /// Whether <paramref name="domain"/> is itself a public suffix, compared without case. A name the list has no rule
    /// for falls under its top-level domain, which is a public suffix.
    /// </summary>
    /// <param name="domain">The domain, without a trailing dot; an internationalized name may be in Unicode or Punycode.</param>
    public static bool IsPublicSuffix(string domain)
        => _rules.Value.IsPublicSuffix(domain);

    /// <summary>
    /// The registrable domain of <paramref name="domain"/>: its public suffix with the one label before it
    /// (<c>example.co.uk</c> for <c>www.example.co.uk</c>), in lower case and Punycode; or <see langword="null"/> when
    /// the name is a public suffix or not a valid name.
    /// </summary>
    /// <param name="domain">The domain, without a trailing dot; an internationalized name may be in Unicode or Punycode.</param>
    public static string GetRegistrableDomain(string domain)
        => _rules.Value.GetRegistrableDomain(domain);

    /// <summary>
    /// Reads rules in the list's format: one rule per line, up to the first white space; lines that are blank or start
    /// with <c>//</c> are skipped.
    /// </summary>
    /// <param name="reader">The list.</param>
    internal static PublicSuffixRules Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var rules = new PublicSuffixRules();

        while (reader.ReadLine() is { } line)
        {
            var text = line.AsSpan().Trim();

            if (text.IsEmpty || text.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            var end = text.IndexOfAny(' ', '\t');

            rules.Add((end < 0 ? text : text[..end]).ToString());
        }

        return rules;
    }

    /// <summary>
    /// The embedded copy of the list, as text.
    /// </summary>
    internal static string ReadEmbeddedList()
    {
        using var stream = typeof(PublicSuffixList).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }

    private static PublicSuffixRules Load()
    {
        using var reader = new StringReader(ReadEmbeddedList());

        return Parse(reader);
    }
}
