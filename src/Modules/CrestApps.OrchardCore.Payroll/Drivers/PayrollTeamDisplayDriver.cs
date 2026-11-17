using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Teams.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// Payroll's card on a team, for people who manage payroll settings: the ledger code its pay costs go to and, for a
/// work site, its tax jurisdictions.
/// </summary>
internal sealed class PayrollTeamDisplayDriver : DisplayDriver<Team>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public PayrollTeamDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override IDisplayResult Edit(Team team, BuildEditorContext context)
        => Initialize<PayrollTeamViewModel>("PayrollTeam_Edit", model =>
        {
            var settings = team.TryGet<PayrollTeamSettings>(out var stored) ? stored : new PayrollTeamSettings();

            model.LedgerCode = settings.LedgerCode;
            model.Jurisdictions = string.Join(", ", settings.Jurisdictions);
        }).Location("Content:8")
        .RenderWhen(() => CanManageAsync());

    public override async Task<IDisplayResult> UpdateAsync(Team team, UpdateEditorContext context)
    {
        if (!await CanManageAsync())
        {
            return null;
        }

        var model = new PayrollTeamViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        team.Put(new PayrollTeamSettings
        {
            LedgerCode = string.IsNullOrWhiteSpace(model.LedgerCode) ? null : model.LedgerCode.Trim(),
            Jurisdictions = Split(model.Jurisdictions),
        });

        return Edit(team, context);
    }

    /// <summary>
    /// Splits a list of jurisdictions typed with commas, spaces or new lines.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The jurisdictions, upper case, each once.</returns>
    internal static IList<string> Split(string text)
        => [.. (text ?? string.Empty)
            .Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)];

    private async Task<bool> CanManageAsync()
        => _httpContextAccessor.HttpContext?.User is { } user && await _authorizationService.AuthorizeAsync(user, PayrollPermissions.ManagePayrollSettings);
}
