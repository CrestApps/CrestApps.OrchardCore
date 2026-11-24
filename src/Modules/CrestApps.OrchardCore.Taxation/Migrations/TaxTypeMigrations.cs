using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Taxation.Migrations;

/// <summary>
/// Seeds the well-known tax types into the catalog, letting operators add, edit, or remove tax types through the admin
/// UI.
/// </summary>
internal sealed class TaxTypeMigrations : DataMigration
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
            await AddAsync(name);
        }

        return 2;
    }

    /// <summary>
    /// Adds the use tax type to a site that was set up before it was a well-known type. A type with the name already is
    /// kept.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await AddAsync(TaxTypeNames.UseTax);

        return 2;
    }

    // The validation refuses a name that is taken, so a type the operator already added is kept.
    private async Task AddAsync(string name)
    {
        var entry = await _manager.NewAsync();
        entry.Name = name;

        var validationResult = await _manager.ValidateAsync(entry);

        if (validationResult.Succeeded)
        {
            await _manager.CreateAsync(entry);
        }
    }
}
