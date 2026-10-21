using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Taxation.Migrations;

/// <summary>
/// Seeds the well-known tax types into the catalog, letting operators add, edit, or remove tax types through the admin
/// UI.
/// </summary>
public sealed class TaxTypeMigrations : DataMigration
{
    private readonly INamedCatalogManager<TaxType> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxTypeMigrations"/> class.
    /// </summary>
    /// <param name="manager">The catalog manager used to create tax types.</param>
    public TaxTypeMigrations(INamedCatalogManager<TaxType> manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// Seeds the well-known tax types.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        foreach (var name in TaxTypeNames.All)
        {
            var entry = await _manager.NewAsync();
            entry.Name = name;

            var validationResult = await _manager.ValidateAsync(entry);

            if (validationResult.Succeeded)
            {
                await _manager.CreateAsync(entry);
            }
        }

        return 1;
    }
}
