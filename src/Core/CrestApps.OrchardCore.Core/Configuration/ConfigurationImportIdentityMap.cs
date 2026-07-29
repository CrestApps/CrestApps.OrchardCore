using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Carries the identifiers that changed while a plan was being imported, for the length of one recipe execution.
/// </summary>
/// <remarks>
/// Configuration refers to configuration by identifier: a queue names its queue group, an entry point names its
/// queue, a subject action names the disposition that triggers it. When an entry in the plan is matched to an entry
/// the destination already had - because the two environments created it independently and it carries the same name -
/// the destination keeps the identifier it already published to its own data, and every reference the rest of the
/// plan carries would otherwise point at an entry that does not exist there. Recording the substitution and applying
/// it to the steps that follow is what keeps a plan meaningful when it is replayed into a tenant that was not empty.
/// The map is keyed by the recipe's execution identifier and held for the tenant rather than for the request, because
/// a recipe step can run in a scope of its own; a scoped service would be discarded between one step and the next and
/// the substitution would be forgotten exactly when the following step needed it.
/// </remarks>
public sealed class ConfigurationImportIdentityMap
{
    private readonly ConcurrentDictionary<string, string> _map = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, JsonObject>> _imported = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, JsonObject> _noneTracked = new(StringComparer.Ordinal);

    /// <summary>
    /// Records that an entry in the plan was imported under a different identifier than the one it was exported with.
    /// </summary>
    /// <param name="exported">The identifier the plan carries.</param>
    /// <param name="stored">The identifier the destination kept.</param>
    public void Record(string exported, string stored)
    {
        if (string.IsNullOrEmpty(exported) || string.IsNullOrEmpty(stored) || string.Equals(exported, stored, StringComparison.Ordinal))
        {
            return;
        }

        _map[exported] = stored;
    }

    /// <summary>
    /// Remembers an entry this import has already stored, so the references it carries can be repaired if an entry
    /// it points at is reconciled later in the same import.
    /// </summary>
    /// <param name="stepName">The recipe step that owns the entry.</param>
    /// <param name="itemId">The identifier the entry was stored under.</param>
    /// <param name="applied">The entry as it was applied, after the substitutions known at the time.</param>
    public void Track(string stepName, string itemId, JsonObject applied)
    {
        if (string.IsNullOrEmpty(stepName) || string.IsNullOrEmpty(itemId) || applied is null)
        {
            return;
        }

        var entries = _imported.GetOrAdd(stepName, _ => new ConcurrentDictionary<string, JsonObject>(StringComparer.Ordinal));

        entries[itemId] = applied;
    }

    /// <summary>
    /// Gets the entries a step has stored during this import, keyed by the identifier they were stored under.
    /// </summary>
    /// <param name="stepName">The recipe step whose entries are wanted.</param>
    /// <returns>The entries as they were applied, or an empty set when the step has stored nothing.</returns>
    public IReadOnlyDictionary<string, JsonObject> GetTracked(string stepName)
    {
        if (!string.IsNullOrEmpty(stepName) && _imported.TryGetValue(stepName, out var entries))
        {
            return entries;
        }

        return _noneTracked;
    }

    /// <summary>
    /// Replaces every recorded identifier anywhere in the given entry, however deeply it is nested.
    /// </summary>
    /// <param name="node">The entry to rewrite in place.</param>
    public void Rewrite(JsonNode node)
    {
        if (node is null || _map.IsEmpty)
        {
            return;
        }

        switch (node)
        {
            case JsonObject entry:
                foreach (var property in entry.ToArray())
                {
                    if (property.Key == nameof(CatalogItem.ItemId))
                    {
                        continue;
                    }

                    if (TryTranslate(property.Value, out var translated))
                    {
                        entry[property.Key] = translated;

                        continue;
                    }

                    Rewrite(property.Value);
                }

                break;

            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    if (TryTranslate(array[index], out var translated))
                    {
                        array[index] = translated;

                        continue;
                    }

                    Rewrite(array[index]);
                }

                break;
        }
    }

    private bool TryTranslate(JsonNode node, out string translated)
    {
        translated = null;

        if (node is not JsonValue value || value.GetValueKind() != System.Text.Json.JsonValueKind.String)
        {
            return false;
        }

        var text = value.GetValue<string>();

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Membership of the map is the filter. Restricting substitution to well-formed identifiers would leave a
        // hand-written plan that names its entries "support-queue" carrying references nothing resolves.
        return _map.TryGetValue(text, out translated);
    }
}
