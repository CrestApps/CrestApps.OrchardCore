using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;

namespace CrestApps.OrchardCore.Payroll.Recipes;

/// <summary>
/// Imports employers, matched by id and then by name.
/// </summary>
internal sealed class EmployersRecipeStep : CatalogRecipeStep<Employer>
{
    public const string EntriesName = "Employers";

    private readonly EmployerStore _store;

    public EmployersRecipeStep(IEmployerManager manager, EmployerStore store)
        : base(PayrollConstants.EmployersRecipeStepName, EntriesName, manager)
    {
        _store = store;
    }

    protected override async ValueTask<Employer> FindExistingAsync(string id, JsonObject data)
    {
        if (await base.FindExistingAsync(id, data) is { } byId)
        {
            return byId;
        }

        var name = data["Name"]?.GetValue<string>();

        return string.IsNullOrWhiteSpace(name) ? null : await _store.FindByNameAsync(name);
    }
}
