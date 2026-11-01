using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// The WorkforceTimeClock recipe step: pay periods, pay codes (matched by code) and time clock policies, with their
/// pay periods by name and the teams that use them.
/// </summary>
internal sealed class TimeClockRecipeStep : NamedRecipeStepHandler
{
    public const string TimeClockStepName = "WorkforceTimeClock";

    private readonly IPayPeriodCalendarManager _calendarManager;
    private readonly IPayCodeManager _payCodeManager;
    private readonly ITimeClockPolicyManager _policyManager;
    private readonly ITeamManager _teamManager;

    public TimeClockRecipeStep(IPayPeriodCalendarManager calendarManager, IPayCodeManager payCodeManager, ITimeClockPolicyManager policyManager, ITeamManager teamManager)
        : base(TimeClockStepName)
    {
        _calendarManager = calendarManager;
        _payCodeManager = payCodeManager;
        _policyManager = policyManager;
        _teamManager = teamManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var import = new RecipeImport(context, TimeClockStepName);
        var calendars = (await _calendarManager.GetAllAsync()).ToList();

        foreach (var (_, incoming) in import.Read<PayPeriodCalendar>("PayPeriods"))
        {
            if (await import.SaveAsync(_calendarManager, incoming, RecipeImport.Existing(calendars, incoming, item => item.Name), $"pay periods '{incoming.Name}'") is { } saved && !calendars.Contains(saved))
            {
                calendars.Add(saved);
            }
        }

        var codes = (await _payCodeManager.GetAllAsync()).ToList();

        foreach (var (_, incoming) in import.Read<PayCode>("PayCodes"))
        {
            if (await import.SaveAsync(_payCodeManager, incoming, RecipeImport.Existing(codes, incoming, item => item.Code), $"pay code '{incoming.Code}'") is { } saved && !codes.Contains(saved))
            {
                codes.Add(saved);
            }
        }

        var policies = (await _policyManager.GetAllAsync()).ToList();
        var teams = (await _teamManager.GetAllAsync()).ToList();

        foreach (var (node, incoming) in import.Read<TimeClockPolicy>("Policies"))
        {
            var ok = true;

            if (RecipeImport.String(node, "payPeriodsName") is { } calendarName)
            {
                var calendar = import.Named(calendars, item => item.Name, calendarName, "policy", incoming.Name, "pay periods");

                ok &= calendar is not null;
                incoming.PayPeriodCalendarId = calendar?.ItemId;
            }

            var usedBy = new List<WorkforceTeam>();

            foreach (var teamName in RecipeImport.Strings(node, "teamNames"))
            {
                var team = import.Named(teams, item => item.Name, teamName, "policy", incoming.Name, "a team");

                ok &= team is not null;

                if (team is not null)
                {
                    usedBy.Add(team);
                }
            }

            if (!ok || await import.SaveAsync(_policyManager, incoming, RecipeImport.Existing(policies, incoming, item => item.Name), $"policy '{incoming.Name}'") is not { } saved)
            {
                continue;
            }

            if (!policies.Contains(saved))
            {
                policies.Add(saved);
            }

            foreach (var team in usedBy.Where(team => team.TimeClockPolicyId != saved.ItemId))
            {
                team.TimeClockPolicyId = saved.ItemId;
                await _teamManager.UpdateAsync(team);
            }
        }
    }
}
