using CrestApps.Core.Models;
using CrestApps.Core.Services;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Records;

/// <summary>
/// Configures a record kind registered with <see cref="AIRecordServiceCollectionExtensions.AddAIRecordKind{T}"/>.
/// A kind needs a description, a permission and a projection; everything else has a default.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
public sealed class AIRecordKindBuilder<T>
    where T : CatalogItem, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIRecordKindBuilder{T}"/> class.
    /// </summary>
    /// <param name="kind">The kind to configure.</param>
    internal AIRecordKindBuilder(AIRecordKind<T> kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// Gets the kind being configured.
    /// </summary>
    public AIRecordKind<T> Kind { get; }

    /// <summary>
    /// Sets the short title of the kind, such as "Tax category".
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> WithTitle(string title)
    {
        Kind.Title = title;

        return this;
    }

    /// <summary>
    /// Sets the description the model reads to choose the kind: what a record is and what it is used for.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> WithDescription(string description)
    {
        Kind.Description = description;

        return this;
    }

    /// <summary>
    /// Sets the permission the caller needs to create, change and delete records; it is also checked for reads
    /// unless <see cref="WithReadPermission"/> sets another one. Use the permission the admin pages check.
    /// </summary>
    /// <param name="permission">The permission.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> WithPermission(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        Kind.Permission = permission;

        return this;
    }

    /// <summary>
    /// Sets the permission the caller needs to list and read records, when reading needs less than writing.
    /// </summary>
    /// <param name="permission">The permission.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> WithReadPermission(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        Kind.ReadPermission = permission;

        return this;
    }

    /// <summary>
    /// Sets the allow-listed fields the tools return for a record, as a new object built from named fields, for
    /// example <c>record =&gt; new { id = record.ItemId, name = record.Name }</c>. Never return the record itself or
    /// its <c>Properties</c>, and never a secret (return <c>hasX = true</c> instead).
    /// </summary>
    /// <param name="projection">The projection.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> WithProjection(Func<T, object> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        Kind.Projection = projection;

        return this;
    }

    /// <summary>
    /// Writes records by replaying the record's admin editor through its display drivers (<c>IDisplayManager</c>),
    /// so the drivers' binding, normalization and validation run as on a form post. This is the default.
    /// </summary>
    /// <param name="groupId">The editor group, when the admin page builds the editor for one; otherwise <see langword="null"/>.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> UseEditorReplay(string groupId = null)
    {
        Kind.CanWrite = true;
        Kind.EditorGroupId = string.IsNullOrWhiteSpace(groupId) ? null : groupId;
        Kind.SaveToolName = null;
        Kind.DeleteToolName = null;

        return this;
    }

    /// <summary>
    /// Lists and reads the records only: <c>saveRecord</c> and <c>deleteRecord</c> refuse the kind. When the owner module
    /// writes the records with a dedicated AI tool (for example because the editor posts lists of objects the editor
    /// replay cannot post), name it, so the refusals and the kind's description send the model to that tool instead of
    /// the admin page.
    /// </summary>
    /// <param name="saveWith">The name of the AI tool that saves the records, or <see langword="null"/> when none does.</param>
    /// <param name="deleteWith">The name of the AI tool that deletes the records, or <see langword="null"/> when none does.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> ReadOnly(string saveWith = null, string deleteWith = null)
    {
        Kind.CanWrite = false;
        Kind.SaveToolName = string.IsNullOrWhiteSpace(saveWith) ? null : saveWith.Trim();
        Kind.DeleteToolName = string.IsNullOrWhiteSpace(deleteWith) ? null : deleteWith.Trim();

        return this;
    }

    /// <summary>
    /// Names editor fields that hold a secret: they are never returned (only whether they are set) and never accepted.
    /// </summary>
    /// <param name="fieldNames">The property names of the editor view model.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> WithSecretFields(params string[] fieldNames)
    {
        foreach (var name in fieldNames ?? [])
        {
            Kind.AddSecretField(name);
        }

        return this;
    }

    /// <summary>
    /// Adds a check run before a record is deleted. It returns why the record cannot be deleted (what still uses
    /// it), or <see langword="null"/> when it can.
    /// </summary>
    /// <param name="inUse">The check.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> WithInUseCheck(Func<IServiceProvider, T, CancellationToken, ValueTask<string>> inUse)
    {
        ArgumentNullException.ThrowIfNull(inUse);

        Kind.InUseAsync = inUse;

        return this;
    }

    /// <summary>
    /// Finds, lists, makes, validates, saves and deletes the records through another catalog manager service than
    /// <c>ICatalogManager&lt;T&gt;</c>, the one the admin pages use, such as <c>INamedCatalogManager&lt;T&gt;</c>.
    /// </summary>
    /// <typeparam name="TManager">The manager service type.</typeparam>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> UseCatalogManager<TManager>()
        where TManager : ICatalogManager<T>
    {
        Kind.UseManager<TManager>();

        return this;
    }

    /// <summary>
    /// Replaces how a record is found by its ID (the default is the catalog manager).
    /// </summary>
    /// <param name="find">Finds a record; <see langword="null"/> when there is none.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> FindUsing(Func<IServiceProvider, string, CancellationToken, ValueTask<T>> find)
    {
        ArgumentNullException.ThrowIfNull(find);

        Kind.FindAsync = find;

        return this;
    }

    /// <summary>
    /// Replaces how a new, unsaved record is made (the default is the catalog manager).
    /// </summary>
    /// <param name="create">Makes a new record.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> NewUsing(Func<IServiceProvider, CancellationToken, ValueTask<T>> create)
    {
        ArgumentNullException.ThrowIfNull(create);

        Kind.NewAsync = create;

        return this;
    }

    /// <summary>
    /// Replaces how a page of records is listed (the default is the catalog manager, filtered by name).
    /// </summary>
    /// <param name="list">Lists a page of records.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> ListUsing(Func<IServiceProvider, AIRecordQuery, CancellationToken, ValueTask<PageResult<T>>> list)
    {
        ArgumentNullException.ThrowIfNull(list);

        Kind.ListAsync = list;

        return this;
    }

    /// <summary>
    /// Replaces how a record is validated after its editor bound the values (the default is the catalog manager).
    /// </summary>
    /// <param name="validate">Validates a record.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> ValidateUsing(Func<IServiceProvider, T, CancellationToken, ValueTask<ValidationResultDetails>> validate)
    {
        ArgumentNullException.ThrowIfNull(validate);

        Kind.ValidateAsync = validate;

        return this;
    }

    /// <summary>
    /// Replaces how a validated record is saved (the default is the catalog manager's create or update).
    /// </summary>
    /// <param name="save">Saves a record; the boolean says whether it is new.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> SaveUsing(Func<IServiceProvider, T, bool, CancellationToken, ValueTask> save)
    {
        ArgumentNullException.ThrowIfNull(save);

        Kind.SaveAsync = save;

        return this;
    }

    /// <summary>
    /// Replaces how a record is deleted (the default is the catalog manager).
    /// </summary>
    /// <param name="delete">Deletes a record; <see langword="false"/> when nothing was deleted.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> DeleteUsing(Func<IServiceProvider, T, CancellationToken, ValueTask<bool>> delete)
    {
        ArgumentNullException.ThrowIfNull(delete);

        Kind.DeleteAsync = delete;

        return this;
    }

    /// <summary>
    /// Replaces how a record is copied before a dry run or a save (the default clones an <c>ICloneable</c> record and
    /// copies any other through JSON).
    /// </summary>
    /// <param name="copy">Copies a record.</param>
    /// <returns>The builder.</returns>
    public AIRecordKindBuilder<T> CopyUsing(Func<T, T> copy)
    {
        ArgumentNullException.ThrowIfNull(copy);

        Kind.Copy = copy;

        return this;
    }
}
