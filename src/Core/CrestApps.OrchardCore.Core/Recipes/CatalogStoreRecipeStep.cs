using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Core.Recipes;

/// <summary>
/// Imports the records of a plain catalog store that a recipe step carries in one array: a record with a known
/// <see cref="CatalogItem.ItemId"/> is updated, any other record is created, so running a recipe twice adds nothing.
/// </summary>
/// <remarks>
/// Use it for records kept in an <see cref="ICatalog{T}"/> without a catalog manager; a manager-backed catalog uses
/// <see cref="CatalogRecipeStep{TEntry}"/>. Records are read and written by <see cref="CatalogRecordSerializer"/>,
/// the same shape <see cref="CatalogStoreDeploymentSource{TStep, TEntry}"/> writes. An update is applied onto the
/// stored record, validated, and saved only when it is valid. Override <see cref="ValidateAsync"/> to check a record
/// the way its admin screen does, and <see cref="Preparing"/> to complete it.
/// </remarks>
/// <typeparam name="TEntry">The record type.</typeparam>
public abstract class CatalogStoreRecipeStep<TEntry> : CatalogRecipeStepBase<TEntry>
    where TEntry : CatalogItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogStoreRecipeStep{TEntry}"/> class.
    /// </summary>
    /// <param name="stepName">The name of the recipe step.</param>
    /// <param name="entriesName">The name of the step member that holds the array of records.</param>
    /// <param name="store">The store the records are kept in.</param>
    protected CatalogStoreRecipeStep(
        string stepName,
        string entriesName,
        ICatalog<TEntry> store)
        : base(stepName, entriesName)
    {
        ArgumentNullException.ThrowIfNull(store);

        Store = store;
    }

    /// <summary>
    /// Gets the store the records are kept in.
    /// </summary>
    protected ICatalog<TEntry> Store { get; }

    /// <inheritdoc/>
    protected override ValueTask<TEntry> FindByIdAsync(string id)
        => Store.FindByIdAsync(id);

    /// <inheritdoc/>
    protected override ValueTask<TEntry> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var entry = CatalogRecordSerializer.Read<TEntry>(token);

        if (entry is not null)
        {
            // The base keeps the identifier the recipe carries when it is valid.
            entry.ItemId = UniqueId.GenerateId();
        }

        return ValueTask.FromResult(entry);
    }

    /// <inheritdoc/>
    /// <remarks>The JSON is applied onto the stored record, which is saved in <see cref="CommitUpdateAsync"/> once it is valid.</remarks>
    protected override ValueTask<TEntry> PrepareUpdateAsync(TEntry entry, JsonObject token)
    {
        CatalogRecordSerializer.Populate(entry, token);

        return ValueTask.FromResult(entry);
    }

    /// <inheritdoc/>
    protected override ValueTask CommitUpdateAsync(TEntry entry, JsonObject token)
        => Store.UpdateAsync(entry);

    /// <inheritdoc/>
    /// <remarks>Every record is valid by default.</remarks>
    protected override ValueTask<ValidationResultDetails> ValidateAsync(TEntry entry)
        => ValueTask.FromResult(new ValidationResultDetails());

    /// <inheritdoc/>
    protected override ValueTask CreateAsync(TEntry entry)
        => Store.CreateAsync(entry);
}
