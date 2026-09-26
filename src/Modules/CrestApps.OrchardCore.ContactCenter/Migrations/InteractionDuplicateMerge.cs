using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Folds the copies of one interaction that an earlier defect stored as separate documents into a single record.
/// </summary>
/// <remarks>
/// Each copy carries part of the call: one was updated by the accept that created it, the other by everything that
/// happened afterwards. The newest copy is the base, and anything it lacks is taken from the older copies, so the
/// merged record is the union of both rather than whichever copy happened to be kept:
/// <list type="bullet">
/// <item>a value the base does not have (a null, an empty text, a missing metadata key) is filled from an older copy;</item>
/// <item>entries in the history and participant lists are combined, without repeating one both copies hold;</item>
/// <item>the legal-hold flag stays set when either copy set it, and the creation time is the earliest recorded;</item>
/// <item>where both copies hold a different value (for example the status), the newer copy's value is kept.</item>
/// </list>
/// Every settable property is visited, so a field added to <see cref="Interaction"/> later is merged too.
/// </remarks>
internal static class InteractionDuplicateMerge
{
    private static readonly PropertyInfo[] _properties = typeof(Interaction)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property =>
            property.CanRead &&
            property.GetSetMethod() is not null &&
            property.GetIndexParameters().Length == 0 &&
            property.GetCustomAttribute<JsonIgnoreAttribute>() is null &&
            property.Name != nameof(Interaction.ItemId))
        .ToArray();

    /// <summary>
    /// Merges the copies into the newest one and returns it.
    /// </summary>
    /// <param name="copiesNewestFirst">The copies of one interaction, the copy to keep first.</param>
    /// <returns>The first copy, now holding what every copy recorded.</returns>
    public static Interaction Merge(IReadOnlyList<Interaction> copiesNewestFirst)
    {
        ArgumentNullException.ThrowIfNull(copiesNewestFirst);

        if (copiesNewestFirst.Count == 0)
        {
            throw new ArgumentException("At least one copy is required.", nameof(copiesNewestFirst));
        }

        var merged = copiesNewestFirst[0];

        foreach (var older in copiesNewestFirst.Skip(1))
        {
            foreach (var property in _properties)
            {
                MergeProperty(property, merged, older);
            }
        }

        return merged;
    }

    /// <summary>
    /// Lists the properties the kept copy and an older copy both hold with different values, which the merge
    /// resolves in favour of the kept copy.
    /// </summary>
    /// <param name="kept">The copy being kept, before the merge.</param>
    /// <param name="older">An older copy.</param>
    /// <returns>The names of the conflicting properties.</returns>
    public static IReadOnlyList<string> Conflicts(Interaction kept, Interaction older)
    {
        ArgumentNullException.ThrowIfNull(kept);
        ArgumentNullException.ThrowIfNull(older);

        var conflicts = new List<string>();

        if (kept.Status != older.Status)
        {
            conflicts.Add(nameof(Interaction.Status));
        }

        foreach (var property in _properties)
        {
            var type = property.PropertyType;

            if (type == typeof(bool) || property.Name is nameof(Interaction.CreatedUtc) or nameof(Interaction.ModifiedUtc))
            {
                continue;
            }

            var keptValue = property.GetValue(kept);
            var olderValue = property.GetValue(older);

            if (IsEmpty(keptValue) || IsEmpty(olderValue) || keptValue is IEnumerable and not string)
            {
                continue;
            }

            if (!Equals(keptValue, olderValue))
            {
                conflicts.Add(property.Name);
            }
        }

        foreach (var entry in older.TechnicalMetadata ?? new Dictionary<string, object>())
        {
            if (kept.TechnicalMetadata?.TryGetValue(entry.Key, out var keptValue) == true &&
                keptValue is not null &&
                entry.Value is not null &&
                !string.Equals(Serialize(keptValue), Serialize(entry.Value), StringComparison.Ordinal))
            {
                conflicts.Add($"{nameof(Interaction.TechnicalMetadata)}.{entry.Key}");
            }
        }

        return conflicts;
    }

    private static void MergeProperty(PropertyInfo property, Interaction merged, Interaction older)
    {
        var type = property.PropertyType;
        var mergedValue = property.GetValue(merged);
        var olderValue = property.GetValue(older);

        if (olderValue is null)
        {
            return;
        }

        if (property.Name == nameof(Interaction.CreatedUtc))
        {
            var olderCreated = (DateTime)olderValue;

            if (olderCreated != default && (merged.CreatedUtc == default || olderCreated < merged.CreatedUtc))
            {
                merged.CreatedUtc = olderCreated;
            }

            return;
        }

        if (type == typeof(bool))
        {
            // A flag either copy raised stays raised; the only one is the legal hold, and losing it would let the
            // retention purge delete a recording the tenant is obliged to keep.
            property.SetValue(merged, (bool)mergedValue || (bool)olderValue);

            return;
        }

        if (olderValue is JsonObject olderObject)
        {
            if (mergedValue is not JsonObject mergedObject)
            {
                property.SetValue(merged, olderObject.DeepClone());

                return;
            }

            foreach (var entry in olderObject)
            {
                if (!mergedObject.ContainsKey(entry.Key) || mergedObject[entry.Key] is null)
                {
                    mergedObject[entry.Key] = entry.Value?.DeepClone();
                }
            }

            return;
        }

        if (olderValue is IDictionary<string, object> olderDictionary)
        {
            if (mergedValue is not IDictionary<string, object> mergedDictionary)
            {
                property.SetValue(merged, new Dictionary<string, object>(olderDictionary));

                return;
            }

            foreach (var entry in olderDictionary)
            {
                if (!mergedDictionary.TryGetValue(entry.Key, out var existing) || existing is null)
                {
                    mergedDictionary[entry.Key] = entry.Value;
                }
            }

            return;
        }

        if (olderValue is IList olderList && olderValue is not string)
        {
            if (mergedValue is not IList mergedList || mergedList.IsReadOnly)
            {
                property.SetValue(merged, olderValue);

                return;
            }

            var present = mergedList.Cast<object>().Select(Serialize).ToHashSet(StringComparer.Ordinal);

            foreach (var item in olderList)
            {
                if (present.Add(Serialize(item)))
                {
                    mergedList.Add(item);
                }
            }

            return;
        }

        if (IsEmpty(mergedValue))
        {
            property.SetValue(merged, olderValue);
        }
    }

    private static bool IsEmpty(object value)
        => value is null || (value is string text && text.Length == 0);

    private static string Serialize(object value)
        => value is null ? "null" : JsonSerializer.Serialize(value, value.GetType());
}
