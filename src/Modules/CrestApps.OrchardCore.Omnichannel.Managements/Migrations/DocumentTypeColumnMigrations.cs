using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Widens the document table's <c>Type</c> column so the catalogs this feature stores can be saved on every engine.
/// </summary>
/// <remarks>
/// Channel endpoints, campaigns and dispositions are each kept as one <c>DictionaryDocument&lt;T&gt;</c>, whose type
/// name carries the record type's assembly-qualified name. The channel endpoint's is 256 characters, one more than the
/// data layer's column holds, so SQL Server refused every new channel endpoint while SQLite stored it.
/// </remarks>
internal sealed class DocumentTypeColumnMigrations : DataMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentTypeColumnMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public DocumentTypeColumnMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Widens the default collection's document <c>Type</c> column.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await DocumentTypeColumnWidening.WidenAsync(SchemaBuilder, _store);

        return 1;
    }
}
