using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Provides the default <see cref="IConfigurationCatalog"/> implementation over any catalog-managed entry type.
/// </summary>
/// <typeparam name="T">The catalog entry type.</typeparam>
public sealed class ConfigurationCatalog<T> : IConfigurationCatalog
    where T : CatalogItem
{
    private readonly ICatalogManager<T> _manager;
    private readonly ConfigurationCatalogDescriptor _descriptor;
    private readonly ConfigurationImportIdentityStore _identities;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationCatalog{T}"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the entries.</param>
    /// <param name="descriptor">The descriptor that names the catalog in recipes and deployment plans.</param>
    /// <param name="identities">The store that carries identifier substitutions across the steps of one recipe.</param>
    public ConfigurationCatalog(
        ICatalogManager<T> manager,
        ConfigurationCatalogDescriptor descriptor,
        ConfigurationImportIdentityStore identities)
    {
        _manager = manager;
        _descriptor = descriptor;
        _identities = identities;
    }

    /// <inheritdoc/>
    public string Group => _descriptor.Group;

    /// <inheritdoc/>
    public string StepName => _descriptor.StepName;

    /// <inheritdoc/>
    public string CollectionName => _descriptor.CollectionName;

    /// <inheritdoc/>
    public int Order => _descriptor.Order;

    /// <inheritdoc/>
    public async Task<JsonArray> ExportAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _manager.GetAllAsync(cancellationToken);
        var result = new JsonArray();

        foreach (var entry in Sort(entries))
        {
            result.Add(ConfigurationCatalogEntryBinder.Export(entry));
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task ImportAsync(JsonArray entries, RecipeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (entries is null || entries.Count == 0)
        {
            return;
        }

        var byName = await BuildNameIndexAsync(cancellationToken);
        var identities = _identities.GetOrCreate(context.ExecutionId);

        foreach (var node in entries.OfType<JsonObject>())
        {
            try
            {
                await ImportEntryAsync(node, byName, identities, context, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                context.Errors.Add($"Unable to import an entry into '{StepName}'. {exception.Message}");
            }
        }
    }

    private async Task ImportEntryAsync(
        JsonObject node,
        Dictionary<string, List<T>> byName,
        ConfigurationImportIdentityMap identities,
        RecipeExecutionContext context,
        CancellationToken cancellationToken)
    {
        // An earlier catalog in the same plan may have landed under an identifier the destination already owned, so
        // the references this entry carries are rewritten before anything reads them.
        identities.Rewrite(node);

        T entry = null;

        var itemId = node[nameof(CatalogItem.ItemId)]?.GetValue<string>();
        var hasItemId = !string.IsNullOrEmpty(itemId);

        if (hasItemId)
        {
            entry = await _manager.FindByIdAsync(itemId, cancellationToken);
        }

        var matchedByKey = false;

        if (entry is null)
        {
            // The index holds what the destination already owned, and each of those entries may be claimed once.
            // Leaving a claimed entry in the index would let a second entry in the same plan match it and overwrite
            // the first, so two entries an operator deliberately kept apart would silently collapse into one.
            matchedByKey = TryClaim(byName, GetAlternateKey(node), out entry);
        }
        else
        {
            Release(byName, GetAlternateKey(entry), entry);
        }

        var isNew = entry is null;

        if (isNew)
        {
            entry = await _manager.NewAsync(node, cancellationToken);

            var pristine = await CreatePristineAsync(node, cancellationToken);

            ConfigurationCatalogEntryBinder.Apply(entry, node, pristine);

            if (hasItemId && UniqueId.IsValid(itemId))
            {
                entry.ItemId = itemId;
            }
            else if (hasItemId)
            {
                // A hand-written plan is free to name its entries whatever it likes, but the store issues the
                // identifiers, so what the plan called this entry has to be translated wherever the plan used it.
                identities.Record(itemId, entry.ItemId);
            }
        }
        else
        {
            ConfigurationCatalogEntryBinder.Apply(entry, node);

            if (matchedByKey && hasItemId && !string.Equals(entry.ItemId, itemId, StringComparison.Ordinal))
            {
                identities.Record(itemId, entry.ItemId);
            }
        }

        var validationResult = await _manager.ValidateAsync(entry, cancellationToken);

        if (!validationResult.Succeeded)
        {
            foreach (var error in validationResult.Errors)
            {
                context.Errors.Add(error.ErrorMessage);
            }

            return;
        }

        if (isNew)
        {
            await _manager.CreateAsync(entry, cancellationToken);
        }
        else
        {
            await _manager.UpdateAsync(entry, node, cancellationToken);
        }

        identities.Track(StepName, entry.ItemId, node.DeepClone().AsObject());
    }

    private static bool TryClaim(Dictionary<string, List<T>> byName, string key, out T entry)
    {
        entry = null;

        if (byName is null || string.IsNullOrEmpty(key) || !byName.TryGetValue(key, out var matches) || matches.Count == 0)
        {
            return false;
        }

        entry = matches[0];
        matches.RemoveAt(0);

        return true;
    }

    private static void Release(Dictionary<string, List<T>> byName, string key, T entry)
    {
        if (byName is null || string.IsNullOrEmpty(key) || !byName.TryGetValue(key, out var matches))
        {
            return;
        }

        matches.Remove(entry);
    }

    /// <inheritdoc/>
    public async Task RepairReferencesAsync(RecipeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var identities = _identities.GetOrCreate(context.ExecutionId);
        var tracked = identities.GetTracked(StepName);

        if (tracked.Count == 0)
        {
            return;
        }

        foreach (var (itemId, applied) in tracked)
        {
            var repaired = applied.DeepClone().AsObject();

            identities.Rewrite(repaired);

            if (JsonNode.DeepEquals(repaired, applied))
            {
                continue;
            }

            try
            {
                await RepairEntryAsync(itemId, repaired, identities, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                context.Errors.Add($"Unable to repair the references of an entry in '{StepName}'. {exception.Message}");
            }
        }
    }

    private async Task RepairEntryAsync(
        string itemId,
        JsonObject repaired,
        ConfigurationImportIdentityMap identities,
        CancellationToken cancellationToken)
    {
        var entry = await _manager.FindByIdAsync(itemId, cancellationToken);

        if (entry is null)
        {
            return;
        }

        ConfigurationCatalogEntryBinder.Apply(entry, repaired);

        var validationResult = await _manager.ValidateAsync(entry, cancellationToken);

        if (!validationResult.Succeeded)
        {
            return;
        }

        await _manager.UpdateAsync(entry, repaired, cancellationToken);

        identities.Track(StepName, itemId, repaired);
    }

    /// <inheritdoc/>
    public string GetIdentity(JsonObject entry)
        => entry is null ? null : GetAlternateKey(entry);

    private async Task<T> CreatePristineAsync(JsonObject node, CancellationToken cancellationToken)
    {
        // The manager is asked for an entry built from nothing but the members it needs to construct one at all. Any
        // member that differs from this on the real entry was derived from the plan by the manager's own handlers,
        // which is how the binder tells a value the manager canonicalised apart from one it merely defaulted.
        var bare = new JsonObject();

        var source = node["Source"];

        if (source is not null)
        {
            bare["Source"] = source.DeepClone();
        }

        try
        {
            return await _manager.NewAsync(bare, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private string GetAlternateKey(T entry)
    {
        if (_descriptor.IdentityProperties is { Length: > 0 })
        {
            return ComposeIdentity(ConfigurationCatalogEntryBinder.Export(entry));
        }

        if (entry is INameAwareModel named)
        {
            return named.Name?.Trim();
        }

        if (entry is IDisplayTextAwareModel display)
        {
            return display.DisplayText?.Trim();
        }

        return null;
    }

    private string GetAlternateKey(JsonObject node)
    {
        if (_descriptor.IdentityProperties is { Length: > 0 })
        {
            return ComposeIdentity(node);
        }

        var name = typeof(INameAwareModel).IsAssignableFrom(typeof(T))
            ? node[nameof(INameAwareModel.Name)]
            : node[nameof(IDisplayTextAwareModel.DisplayText)];

        return name?.GetValue<string>()?.Trim();
    }

    private string ComposeIdentity(JsonObject node)
    {
        var parts = new List<string>(_descriptor.IdentityProperties.Length);

        foreach (var property in _descriptor.IdentityProperties)
        {
            var value = node?[property];

            if (value is null)
            {
                continue;
            }

            var text = value.GetValueKind() == JsonValueKind.String
                ? value.GetValue<string>()
                : value.ToJsonString();

            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add($"{property}={text.Trim()}");
            }
        }

        return parts.Count == 0
            ? null
            : string.Join('|', parts);
    }

    private async Task<Dictionary<string, List<T>>> BuildNameIndexAsync(CancellationToken cancellationToken)
    {
        if (_descriptor.IdentityProperties is not { Length: > 0 }
            && !typeof(INameAwareModel).IsAssignableFrom(typeof(T))
            && !typeof(IDisplayTextAwareModel).IsAssignableFrom(typeof(T)))
        {
            return null;
        }

        var index = new Dictionary<string, List<T>>(StringComparer.OrdinalIgnoreCase);

        // Nothing stops an operator from keeping two entries under one name, so the index has to hold all of them in
        // a settled order. Claiming them one at a time in that order is what makes a replay land on the same entries
        // it landed on the first time.
        foreach (var entry in Sort(await _manager.GetAllAsync(cancellationToken)))
        {
            var key = GetAlternateKey(entry);

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (!index.TryGetValue(key, out var matches))
            {
                matches = [];
                index[key] = matches;
            }

            matches.Add(entry);
        }

        return index;
    }

    private IEnumerable<T> Sort(IEnumerable<T> entries)
        => entries.OrderBy(GetAlternateKey, StringComparer.Ordinal).ThenBy(x => x.ItemId, StringComparer.Ordinal);
}
