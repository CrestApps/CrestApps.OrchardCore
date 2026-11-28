using CrestApps.Core.AI;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.PointOfSale.AI;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PointOfSale;

/// <summary>
/// Registers the Point of Sale AI tools when the Point of Sale and AI features are on, all under
/// <c>ManageRegisters</c> as Commerce › Point of sale: <c>listRegisterShifts</c>, and the registers and departments as
/// read-only kinds of the generic record tools (<c>posRegister</c>, <c>posDepartment</c>). Their admin pages bind their
/// own view models instead of display drivers, so the editor replay cannot write them yet; they are changed on the pages
/// or with the Registers and PosDepartments recipe steps.
/// </summary>
[Feature(PosConstants.Feature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListRegisterShiftsTool>(ListRegisterShiftsTool.TheName)
            .WithTitle(S["List Register Shifts"])
            .WithDescription(S["Lists register shifts, optionally for one register, with who opened and closed them, when, and the opening float; or returns one shift in full with its cash movements, sales count and totals by cash, card and other, refunds, tax, totals by department, expected against counted cash, and the card and cash tips the cashier declared (and whether payroll took them). Use it to check a till's takings or a cash difference."])
            .WithCategory(S["Point of Sale"])
            .Selectable();

        services.AddAIRecordKind<Register>("posRegister", kind => kind
            .WithTitle(S["Register"])
            .WithDescription(S["a point of sale register (till): the stock location it sells from, the store address that taxes its sales, its currency, the roles whose members may sell at it, whether it is enabled, and its department keys. Read-only here; registers are changed on Commerce › Point of sale › Registers"])
            .WithPermission(PosPermissions.ManageRegisters)
            .FindUsing((services, id, cancellationToken) => services.GetRequiredService<IRegisterStore>().FindByIdAsync(id, cancellationToken))
            .ListUsing((services, query, cancellationToken) => ListAsync(services.GetRequiredService<IRegisterStore>(), register => register.Name, query, cancellationToken))
            .WithProjection(register => new
            {
                id = register.ItemId,
                name = register.Name,
                locationId = register.LocationId,
                address = register.Address is { } address
                    ? new { line1 = address.AddressLine1, line2 = address.AddressLine2, city = address.City, region = address.Region, postalCode = address.PostalCode, country = address.Country }
                    : null,
                currency = register.Currency,
                roles = register.Roles,
                isEnabled = register.IsEnabled,
                keys = register.Keys.Select(key => new { departmentId = key.DepartmentId, label = key.Label, presetAmount = key.PresetAmount }),
            })
            .ReadOnly());

        services.AddAIRecordKind<PosDepartment>("posDepartment", kind => kind
            .WithTitle(S["Point of sale department"])
            .WithDescription(S["a department registers ring amounts up in (such as Gift wrapping), with its number, tax category, color, minimum and maximum amount, position and enabled flag. Read-only here; departments are changed on Commerce › Point of sale › Departments"])
            .WithPermission(PosPermissions.ManageRegisters)
            .FindUsing((services, id, cancellationToken) => services.GetRequiredService<IPosDepartmentStore>().FindByIdAsync(id, cancellationToken))
            .ListUsing((services, query, cancellationToken) => ListAsync(services.GetRequiredService<IPosDepartmentStore>(), department => department.Name, query, cancellationToken))
            .WithProjection(department => new
            {
                id = department.ItemId,
                name = department.Name,
                number = department.Number,
                taxCategoryCode = department.TaxCategoryCode,
                color = department.Color,
                minimumAmount = department.MinimumAmount,
                maximumAmount = department.MaximumAmount,
                position = department.Position,
                isEnabled = department.IsEnabled,
            })
            .ReadOnly());
    }

    // The register and department lists are short setup catalogs: filtered by name and paged in memory, in name order.
    private static async ValueTask<PageResult<T>> ListAsync<T>(ICatalog<T> catalog, Func<T, string> name, AIRecordQuery query, CancellationToken cancellationToken)
        where T : CatalogItem
    {
        var matches = (await catalog.GetAllAsync(cancellationToken))
            .Where(entry => string.IsNullOrWhiteSpace(query.Text) || (name(entry)?.Contains(query.Text.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(entry => name(entry), StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PageResult<T>
        {
            Count = matches.Count,
            Entries = matches.Skip((Math.Max(1, query.Page) - 1) * query.PageSize).Take(query.PageSize).ToList(),
        };
    }
}
