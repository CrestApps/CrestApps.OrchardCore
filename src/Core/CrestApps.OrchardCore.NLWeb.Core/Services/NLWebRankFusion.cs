using CrestApps.OrchardCore.NLWeb.Models;

namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Merges the ranked lists of several retrievers with reciprocal rank fusion: an item scores
/// <c>1 / (k + rank)</c> in each list it is in, and the sum orders the merged list.
/// </summary>
public static class NLWebRankFusion
{
    /// <summary>
    /// The rank constant. 60 is the value commonly used for reciprocal rank fusion.
    /// </summary>
    public const int RankConstant = 60;

    /// <summary>
    /// Merges ranked lists of candidates.
    /// </summary>
    /// <param name="lists">The lists, each best first.</param>
    /// <param name="count">The most candidates to return.</param>
    /// <returns>The merged list, best first.</returns>
    public static IReadOnlyList<NLWebCandidate> Fuse(IReadOnlyList<IReadOnlyList<NLWebCandidate>> lists, int count)
    {
        ArgumentNullException.ThrowIfNull(lists);

        if (lists.Count == 1)
        {
            return Distinct(lists[0], count);
        }

        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        var firstSeen = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = 0;

        foreach (var list in lists)
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                var candidate = list[rank];

                if (string.IsNullOrEmpty(candidate?.Id))
                {
                    continue;
                }

                scores[candidate.Id] = scores.GetValueOrDefault(candidate.Id) + (1d / (RankConstant + rank + 1));

                if (firstSeen.TryAdd(candidate.Id, order++))
                {
                    sources[candidate.Id] = candidate.Source;
                }
                else if (sources[candidate.Id] != candidate.Source)
                {
                    sources[candidate.Id] = $"{sources[candidate.Id]}+{candidate.Source}";
                }
            }
        }

        return scores
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => firstSeen[pair.Key])
            .Take(count)
            .Select(pair => new NLWebCandidate(pair.Key, pair.Value, sources[pair.Key]))
            .ToList();
    }

    private static List<NLWebCandidate> Distinct(IReadOnlyList<NLWebCandidate> list, int count)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<NLWebCandidate>();

        foreach (var candidate in list)
        {
            if (string.IsNullOrEmpty(candidate?.Id) || !seen.Add(candidate.Id))
            {
                continue;
            }

            result.Add(candidate);

            if (result.Count == count)
            {
                break;
            }
        }

        return result;
    }
}
