using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;

namespace CrestApps.OrchardCore.Geo.Core.Services;

/// <summary>
/// Finds the territory of a point or postal code. Pure.
/// </summary>
public static class TerritoryMatcher
{
    /// <summary>
    /// Writes a postal code the way territories keep them: upper case, without spaces or dashes.
    /// </summary>
    /// <param name="postalCode">The postal code.</param>
    /// <returns>The code, or an empty string.</returns>
    public static string NormalizePostalCode(string postalCode)
        => string.IsNullOrWhiteSpace(postalCode) ? string.Empty : string.Concat(postalCode.Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant();

    /// <summary>
    /// Finds the territory: the one with the smallest shape the point is in, else the one with the postal code (an
    /// exact code before the longest prefix).
    /// </summary>
    /// <param name="territories">The territories.</param>
    /// <param name="point">The point, or <see langword="null"/>.</param>
    /// <param name="postalCode">The postal code, or <see langword="null"/>.</param>
    /// <returns>The match, or <see langword="null"/>.</returns>
    public static TerritoryMatch Match(IEnumerable<ServiceTerritory> territories, GeoPoint? point, string postalCode)
    {
        ArgumentNullException.ThrowIfNull(territories);

        var list = territories.ToList();

        if (point is { IsValid: true } at)
        {
            var inside = list
                .Where(territory => !string.IsNullOrEmpty(territory.Shape))
                .Select(territory => (Territory: territory, Shape: GeoShape.TryParse(territory.Shape, out var shape, out _) ? shape : null))
                .Where(entry => entry.Shape is not null && entry.Shape.Contains(at))
                .OrderBy(entry => entry.Shape.Area)
                .FirstOrDefault();

            if (inside.Territory is not null)
            {
                return new TerritoryMatch(inside.Territory, TerritoryMatchKind.Shape);
            }
        }

        var code = NormalizePostalCode(postalCode);

        if (code.Length == 0)
        {
            return null;
        }

        var best = list
            .SelectMany(territory => territory.PostalCodes.Select(entry => (Territory: territory, Score: Score(NormalizePostalCode(entry.TrimEnd('*')), entry.EndsWith('*'), code))))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .FirstOrDefault();

        return best.Territory is null ? null : new TerritoryMatch(best.Territory, TerritoryMatchKind.PostalCode);
    }

    // An exact code beats every prefix; a longer prefix beats a shorter one.
    private static int Score(string entry, bool isPrefix, string code)
    {
        if (entry.Length == 0)
        {
            return 0;
        }

        if (!isPrefix)
        {
            return entry == code ? int.MaxValue : 0;
        }

        return code.StartsWith(entry, StringComparison.Ordinal) ? entry.Length : 0;
    }
}

/// <summary>
/// Finds the territory of a point or postal code among the stored territories.
/// </summary>
public sealed class DefaultTerritoryResolver : ITerritoryResolver
{
    private readonly ServiceTerritoryStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTerritoryResolver"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    public DefaultTerritoryResolver(ServiceTerritoryStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<TerritoryMatch> ResolveAsync(GeoPoint? point, string postalCode, CancellationToken cancellationToken = default)
        => TerritoryMatcher.Match(await _store.GetAllAsync(cancellationToken), point, postalCode);
}
