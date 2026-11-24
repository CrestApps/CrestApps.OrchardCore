using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;

namespace CrestApps.OrchardCore.Payroll.Recipes;

/// <summary>
/// Imports pay schedules, matched by id and then by name.
/// </summary>
internal sealed class PaySchedulesRecipeStep : CatalogRecipeStep<PaySchedule>
{
    public const string EntriesName = "Schedules";

    private readonly IPayScheduleManager _manager;

    public PaySchedulesRecipeStep(IPayScheduleManager manager)
        : base(PayrollConstants.PaySchedulesRecipeStepName, EntriesName, manager)
    {
        _manager = manager;
    }

    protected override async ValueTask<PaySchedule> FindExistingAsync(string id, JsonObject data)
    {
        if (await base.FindExistingAsync(id, data) is { } byId)
        {
            return byId;
        }

        var name = data["Name"]?.GetValue<string>()?.Trim();

        return string.IsNullOrEmpty(name) ? null : (await _manager.GetAllAsync()).FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Imports pay codes, matched by id and then by code.
/// </summary>
internal sealed class PayCodesRecipeStep : CatalogRecipeStep<PayCode>
{
    public const string EntriesName = "Codes";

    private readonly IPayCodeManager _manager;

    public PayCodesRecipeStep(IPayCodeManager manager)
        : base(PayrollConstants.PayCodesRecipeStepName, EntriesName, manager)
    {
        _manager = manager;
    }

    protected override async ValueTask<PayCode> FindExistingAsync(string id, JsonObject data)
    {
        if (await base.FindExistingAsync(id, data) is { } byId)
        {
            return byId;
        }

        var code = data["Code"]?.GetValue<string>();

        return string.IsNullOrWhiteSpace(code) ? null : await _manager.FindByCodeAsync(code);
    }
}
