using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// The WorkforceTimeClock recipe step: pay periods, pay codes (matched by code) and time clock policies, with their
/// pay periods by name and the teams that use them; time clock kiosks, with their team by name; and the columns of the
/// payroll CSV file.
/// </summary>
/// <remarks>
/// A kiosk's sign-in token is never carried: an imported kiosk keeps the token it has, and a new kiosk gets one when an
/// administrator issues it.
/// </remarks>
internal sealed class TimeClockRecipeStep : NamedRecipeStepHandler
{
    public const string TimeClockStepName = "WorkforceTimeClock";

    private readonly IPayPeriodCalendarManager _calendarManager;
    private readonly IPayCodeManager _payCodeManager;
    private readonly ITimeClockPolicyManager _policyManager;
    private readonly ITeamManager _teamManager;
    private readonly ITimeClockKioskManager _kioskManager;
    private readonly IPayrollExportService _payrollExportService;

    public TimeClockRecipeStep(
        IPayPeriodCalendarManager calendarManager,
        IPayCodeManager payCodeManager,
        ITimeClockPolicyManager policyManager,
        ITeamManager teamManager,
        ITimeClockKioskManager kioskManager,
        IPayrollExportService payrollExportService)
        : base(TimeClockStepName)
    {
        _calendarManager = calendarManager;
        _payCodeManager = payCodeManager;
        _policyManager = policyManager;
        _teamManager = teamManager;
        _kioskManager = kioskManager;
        _payrollExportService = payrollExportService;
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

            var usedBy = new List<Team>();

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

            foreach (var team in usedBy.Where(team => team.GetWorkforceSettings().TimeClockPolicyId != saved.ItemId))
            {
                team.GetWorkforceSettings().TimeClockPolicyId = saved.ItemId;
                await _teamManager.UpdateAsync(team);
            }
        }

        await ImportKiosksAsync(import, context, teams);
        await ImportPayrollCsvAsync(context);
    }

    private async Task ImportKiosksAsync(RecipeImport import, RecipeExecutionContext context, List<Team> teams)
    {
        var kiosks = (await _kioskManager.GetAllAsync()).ToList();

        foreach (var (node, incoming) in import.Read<TimeClockKiosk>("Kiosks"))
        {
            string teamId = null;

            if (RecipeImport.String(node, "teamName") is { } teamName)
            {
                var team = import.Named(teams, item => item.Name, teamName, "kiosk", incoming.Name, "a team");

                if (team is null)
                {
                    continue;
                }

                teamId = team.ItemId;
            }

            var existing = RecipeImport.Existing(kiosks, incoming, item => item.Name);
            var target = existing ?? await _kioskManager.NewAsync();

            if (existing is null && UniqueId.IsValid(incoming.ItemId))
            {
                target.ItemId = incoming.ItemId;
            }

            // Only the configuration is applied: the token hash and the last use belong to the site that issued the kiosk.
            target.Name = incoming.Name;
            target.TeamId = teamId;
            target.Enabled = incoming.Enabled;

            var validation = await _kioskManager.ValidateAsync(target);

            if (!validation.Succeeded)
            {
                context.Errors.Add($"The kiosk '{incoming.Name}' in the {TimeClockStepName} step was refused: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");

                continue;
            }

            if (existing is null)
            {
                await _kioskManager.CreateAsync(target);
                kiosks.Add(target);
            }
            else
            {
                await _kioskManager.UpdateAsync(target);
            }
        }
    }

    private async Task ImportPayrollCsvAsync(RecipeExecutionContext context)
    {
        if (context.Step["PayrollCsv"] is not JsonObject node)
        {
            return;
        }

        PayrollCsvSettings incoming;

        try
        {
            incoming = node.Deserialize<PayrollCsvSettings>(RecipeImport.JsonOptions);
        }
        catch (JsonException ex)
        {
            context.Errors.Add($"The PayrollCsv entry in the {TimeClockStepName} step can't be read: {ex.Message}");

            return;
        }

        if (incoming is null)
        {
            return;
        }

        var settings = await _payrollExportService.GetCsvSettingsAsync();

        settings.Columns = incoming.Columns.Where(column => !string.IsNullOrWhiteSpace(column.Header)).ToList();

        await _payrollExportService.SaveCsvSettingsAsync(settings);
    }
}
