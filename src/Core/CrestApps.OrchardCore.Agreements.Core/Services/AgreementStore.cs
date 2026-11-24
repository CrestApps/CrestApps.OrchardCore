using System.Globalization;
using CrestApps.OrchardCore.Agreements.Core.Indexes;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Agreements.Core.Services;

/// <summary>
/// Stores agreements.
/// </summary>
public sealed class AgreementStore : DocumentCatalog<Agreement, AgreementIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgreementStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public AgreementStore(ISession session)
        : base(session)
    {
        CollectionName = AgreementsConstants.CollectionName;
    }

    /// <summary>
    /// Finds an agreement by its number, the one rule every caller uses: the number as written (SA-00042, in any case),
    /// or just its digits (42).
    /// </summary>
    /// <param name="number">The number.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The agreement, or <see langword="null"/>.</returns>
    public async ValueTask<Agreement> FindByNumberAsync(string number, CancellationToken cancellationToken = default)
    {
        if (NormalizeNumber(number) is not { } normalized)
        {
            return null;
        }

        var agreement = await Session.Query<Agreement, AgreementIndex>(index => index.Number == normalized, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        return agreement is null ? null : await LoadedAsync(agreement);
    }

    /// <summary>
    /// Writes a number as agreements are numbered: digits alone get the prefix and five digits (42 is SA-00042), and any
    /// other text is upper-cased.
    /// </summary>
    /// <param name="number">The number as given.</param>
    /// <returns>The number as stored, or <see langword="null"/> for an empty text.</returns>
    public static string NormalizeNumber(string number)
    {
        var text = number?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.All(char.IsAsciiDigit) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? AgreementsConstants.NumberPrefix + value.ToString("D5", CultureInfo.InvariantCulture)
            : text.ToUpperInvariant();
    }
}
