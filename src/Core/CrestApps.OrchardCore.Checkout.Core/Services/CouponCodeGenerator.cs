using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Numbering;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Makes the unique codes generated for a coupon, the one rule both generating codes in bulk and issuing one code to a
/// person use.
/// </summary>
public static class CouponCodeGenerator
{
    private const int CodeLength = 8;
    private const int Attempts = 5;
    private const int QueryChunkSize = 500;

    /// <summary>
    /// Returns the prefix as codes carry it: trimmed and in upper case, or an empty text when there is none.
    /// </summary>
    /// <param name="prefix">The prefix as it was entered.</param>
    public static string NormalizePrefix(string prefix)
        => prefix?.Trim().ToUpperInvariant() ?? string.Empty;

    /// <summary>
    /// Returns whether a prefix is empty, or up to <see cref="CheckoutConstants.MaxCouponCodePrefixLength"/> letters,
    /// digits or dashes.
    /// </summary>
    /// <param name="prefix">The prefix as it was entered.</param>
    public static bool IsValidPrefix(string prefix)
    {
        var normalized = NormalizePrefix(prefix);

        return normalized.Length <= CheckoutConstants.MaxCouponCodePrefixLength &&
            normalized.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    }

    // Codes are random enough that clashes are rare; the few there are, with each other or with codes already in use
    // (a coupon's own code or another generated code), are drawn again. Fewer than asked come back only when every
    // attempt clashed.
    internal static async Task<IReadOnlyList<string>> DrawUniqueAsync(ISession session, string prefix, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        prefix = NormalizePrefix(prefix);

        var codes = new HashSet<string>(StringComparer.Ordinal);

        for (var attempt = 0; attempt < Attempts && codes.Count < count; attempt++)
        {
            var candidates = new HashSet<string>(StringComparer.Ordinal);

            while (candidates.Count < count - codes.Count)
            {
                var code = prefix + ReadableCodes.Generate(CodeLength);

                if (!codes.Contains(code))
                {
                    candidates.Add(code);
                }
            }

            foreach (var chunk in candidates.Chunk(QueryChunkSize))
            {
                var taken = (await session.QueryIndex<CouponIndex>(x => x.NormalizedCode.IsIn(chunk), collection: CheckoutConstants.CouponCollectionName).ListAsync(cancellationToken))
                    .Select(index => index.NormalizedCode)
                    .Concat((await session.QueryIndex<CouponCodeIndex>(x => x.NormalizedCode.IsIn(chunk), collection: CheckoutConstants.CouponCollectionName).ListAsync(cancellationToken))
                        .Select(index => index.NormalizedCode))
                    .ToHashSet(StringComparer.Ordinal);

                codes.UnionWith(chunk.Where(code => !taken.Contains(code)));
            }
        }

        return [.. codes.Take(count)];
    }
}
