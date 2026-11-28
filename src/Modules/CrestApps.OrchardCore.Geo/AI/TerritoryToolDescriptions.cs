using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;

namespace CrestApps.OrchardCore.Geo.AI;

/// <summary>
/// Builds what the territory tools return about a territory, field by field.
/// </summary>
internal static class TerritoryToolDescriptions
{
    /// <summary>
    /// The most postal codes a territory lists in a summary.
    /// </summary>
    public const int MaxPostalCodes = 25;

    /// <summary>
    /// Reads who can serve territories, by kind, from the member sources other modules register.
    /// </summary>
    /// <param name="sources">The member sources.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Each kind's label and its members' names by id.</returns>
    public static async Task<Dictionary<string, (string DisplayName, Dictionary<string, string> Names)>> ReadMemberSourcesAsync(IEnumerable<ITerritoryMemberSource> sources, CancellationToken cancellationToken)
    {
        var kinds = new Dictionary<string, (string DisplayName, Dictionary<string, string> Names)>(StringComparer.Ordinal);

        foreach (var source in sources ?? [])
        {
            if (string.IsNullOrEmpty(source.Kind) || kinds.ContainsKey(source.Kind))
            {
                continue;
            }

            var names = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var (id, name) in await source.ListAsync(cancellationToken))
            {
                if (!string.IsNullOrEmpty(id))
                {
                    names.TryAdd(id, name);
                }
            }

            kinds[source.Kind] = (source.DisplayName, names);
        }

        return kinds;
    }

    /// <summary>
    /// Describes a territory.
    /// </summary>
    /// <param name="territory">The territory.</param>
    /// <param name="territoryNames">The names of all territories, by id, for the parent.</param>
    /// <param name="memberSources">The member sources, by kind.</param>
    /// <returns>The description.</returns>
    public static object Describe(
        ServiceTerritory territory,
        IReadOnlyDictionary<string, string> territoryNames,
        IReadOnlyDictionary<string, (string DisplayName, Dictionary<string, string> Names)> memberSources)
    {
        ArgumentNullException.ThrowIfNull(territory);

        var postalCodes = territory.PostalCodes ?? [];

        return new
        {
            id = territory.ItemId,
            name = territory.Name,
            parentId = territory.ParentId,
            parentName = territory.ParentId is null ? null : territoryNames?.GetValueOrDefault(territory.ParentId),
            hasShape = !string.IsNullOrEmpty(territory.Shape),
            postalCodes = postalCodes.Take(MaxPostalCodes).ToList(),
            postalCodeCount = postalCodes.Count,
            timeZoneId = territory.TimeZoneId,
            travelSpeedKmh = territory.TravelSpeedKmh,
            members = (territory.Members ?? [])
                .Select(member => new
                {
                    kind = member.Kind,
                    id = member.Id,
                    name = memberSources is not null && member.Kind is not null && memberSources.TryGetValue(member.Kind, out var source)
                        ? source.Names.GetValueOrDefault(member.Id)
                        : null,
                    role = member.Role.ToString(),
                })
                .ToList(),
        };
    }
}
