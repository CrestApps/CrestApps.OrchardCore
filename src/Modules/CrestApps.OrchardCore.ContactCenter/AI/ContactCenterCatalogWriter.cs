using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Creates or updates a Contact Center catalog record the way the record's public API and recipe step do: the manager
/// builds the record from JSON through its handlers, the handlers validate it, and only then is it saved. An update is
/// validated on a copy first, because the manager saves as it updates.
/// </summary>
internal static class ContactCenterCatalogWriter
{
    private const string PropertiesKey = nameof(CatalogItem.Properties);

    /// <summary>
    /// Creates a record from the changes, or updates an existing record with them.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="manager">The record's manager.</param>
    /// <param name="existing">The record to update, or <see langword="null"/> to create one.</param>
    /// <param name="changes">The changes, keyed by the record's property names. A property left out is kept.</param>
    /// <param name="normalize">The record's editor rules (trimming, clamping, clearing what does not apply), run before validation.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <param name="copyOf">When creating, the record the new one copies, as the admin pages' Clone action does: every
    /// setting, its Properties included, before the changes are applied.</param>
    /// <returns>The saved record, or the reasons nothing was saved.</returns>
    public static async Task<CatalogWriteResult<T>> SaveAsync<T>(
        ICatalogManager<T> manager,
        T existing,
        JsonObject changes,
        Action<T> normalize,
        CancellationToken cancellationToken,
        T copyOf = null)
        where T : CatalogItem, new()
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(changes);

        // Settings other features keep on the record live in its Properties; the tools never write them.
        changes.Remove(PropertiesKey);
        changes.Remove(nameof(CatalogItem.ItemId));

        try
        {
            if (existing is null)
            {
                var data = copyOf is null ? new JsonObject() : CatalogDeploymentSerializer.Export(copyOf);
                data.Remove(nameof(CatalogItem.ItemId));

                foreach (var (key, value) in changes)
                {
                    data[key] = value?.DeepClone();
                }

                // The handlers read the data with the deployment serializer and their errors are logged, not thrown, so
                // a value of the wrong shape is caught here rather than saved as nothing.
                EnsureReadable<T>(data);

                var created = await manager.NewAsync(data, cancellationToken);
                normalize?.Invoke(created);

                var validation = await manager.ValidateAsync(created, cancellationToken);

                if (!validation.Succeeded)
                {
                    return CatalogWriteResult<T>.Invalid(validation);
                }

                await manager.CreateAsync(created, cancellationToken);

                return CatalogWriteResult<T>.Saved(created);
            }

            // The record as it would be after the change: the current record with the changed properties replaced.
            var merged = CatalogDeploymentSerializer.Export(existing);

            foreach (var (key, value) in changes)
            {
                merged[key] = value?.DeepClone();
            }

            EnsureReadable<T>(merged);

            var copy = await manager.NewAsync(merged, cancellationToken);
            copy.ItemId = existing.ItemId;
            normalize?.Invoke(copy);

            var copyValidation = await manager.ValidateAsync(copy, cancellationToken);

            if (!copyValidation.Succeeded)
            {
                return CatalogWriteResult<T>.Invalid(copyValidation);
            }

            // The validated copy's settings, never its Properties, are what the record is updated with, so the rules the
            // copy went through are the ones saved and what other features keep on the record is left alone.
            var body = CatalogDeploymentSerializer.Export(copy);
            body.Remove(PropertiesKey);
            body.Remove(nameof(CatalogItem.ItemId));

            await manager.UpdateAsync(existing, body, cancellationToken);

            return CatalogWriteResult<T>.Saved(existing);
        }
        catch (JsonException exception)
        {
            return CatalogWriteResult<T>.Invalid($"A value has the wrong shape: {exception.Message}");
        }
    }

    private static void EnsureReadable<T>(JsonObject data)
        where T : CatalogItem, new()
        => _ = data.Deserialize<T>(CatalogDeploymentSerializer.Options);
}

/// <summary>
/// The outcome of <see cref="ContactCenterCatalogWriter.SaveAsync{T}"/>.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
internal sealed class CatalogWriteResult<T>
    where T : CatalogItem
{
    private CatalogWriteResult(T record, IReadOnlyList<string> errors)
    {
        Record = record;
        Errors = errors;
    }

    /// <summary>
    /// Gets the saved record.
    /// </summary>
    public T Record { get; }

    /// <summary>
    /// Gets the reasons nothing was saved.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Gets whether the record was saved.
    /// </summary>
    public bool Succeeded
        => Record is not null;

    /// <summary>
    /// Gets the reasons as one sentence for the model.
    /// </summary>
    public string Message
        => "Nothing was saved: " + string.Join(" ", Errors);

    /// <summary>
    /// A saved record.
    /// </summary>
    public static CatalogWriteResult<T> Saved(T record)
        => new(record, []);

    /// <summary>
    /// A record the handlers refused.
    /// </summary>
    public static CatalogWriteResult<T> Invalid(ValidationResultDetails validation)
        => new(null, validation.Errors
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .DefaultIfEmpty("the record is not valid.")
            .ToList());

    /// <summary>
    /// A record that could not be built.
    /// </summary>
    public static CatalogWriteResult<T> Invalid(string message)
        => new(null, [message]);
}
