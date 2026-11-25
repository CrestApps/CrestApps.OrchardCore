using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// Settings &gt; Payroll: the ledger accounts of the journal entries, by default and for a pay code or a tax.
/// </summary>
internal sealed class PayrollJournalSettingsDisplayDriver : SiteDisplayDriver<PayrollJournalSettings>
{
    private const int BlankRows = 3;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public PayrollJournalSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<PayrollJournalSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => PayrollConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, PayrollJournalSettings settings, BuildEditorContext context)
        => Combine(
            Initialize<PayrollJournalSettingsViewModel>("PayrollJournalSettings_Edit", model => Populate(settings, model))
                .Location(l => l.Zone("Content", "5").Card(S["Journal Accounts"], "40"))
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, PayrollPermissions.ManagePayrollSettings))
                .OnGroup(SettingsGroupId),
            Initialize<PayrollJournalSettingsViewModel>("PayrollJournalMappings_Edit", model => Populate(settings, model))
                .Location(l => l.Zone("Content", "5").Card(S["Pay Code and Tax Accounts"], "50"))
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, PayrollPermissions.ManagePayrollSettings))
                .OnGroup(SettingsGroupId));

    public override async Task<IDisplayResult> UpdateAsync(ISite site, PayrollJournalSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, PayrollPermissions.ManagePayrollSettings))
        {
            return null;
        }

        var model = new PayrollJournalSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        static string Clean(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        settings.NetPayAccount = Clean(model.NetPayAccount);
        settings.WageExpenseAccount = Clean(model.WageExpenseAccount);
        settings.DeductionLiabilityAccount = Clean(model.DeductionLiabilityAccount);
        settings.TaxLiabilityAccount = Clean(model.TaxLiabilityAccount);
        settings.EmployerTaxExpenseAccount = Clean(model.EmployerTaxExpenseAccount);
        settings.ContributionExpenseAccount = Clean(model.ContributionExpenseAccount);
        settings.ContributionLiabilityAccount = Clean(model.ContributionLiabilityAccount);
        settings.ReimbursementExpenseAccount = Clean(model.ReimbursementExpenseAccount);

        // A row without a key is a blank row; one key once.
        settings.Mappings = [.. (model.Mappings ?? [])
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping?.Key))
            .GroupBy(mapping => mapping.Key.Trim().ToUpperInvariant(), StringComparer.Ordinal)
            .Select(group => new PayrollJournalMapping { Key = group.Key, DebitAccount = Clean(group.Last().DebitAccount), CreditAccount = Clean(group.Last().CreditAccount) })];

        return Edit(site, settings, context);
    }

    private static void Populate(PayrollJournalSettings settings, PayrollJournalSettingsViewModel model)
    {
        model.NetPayAccount = settings.NetPayAccount;
        model.WageExpenseAccount = settings.WageExpenseAccount;
        model.DeductionLiabilityAccount = settings.DeductionLiabilityAccount;
        model.TaxLiabilityAccount = settings.TaxLiabilityAccount;
        model.EmployerTaxExpenseAccount = settings.EmployerTaxExpenseAccount;
        model.ContributionExpenseAccount = settings.ContributionExpenseAccount;
        model.ContributionLiabilityAccount = settings.ContributionLiabilityAccount;
        model.ReimbursementExpenseAccount = settings.ReimbursementExpenseAccount;
        model.Mappings = [.. settings.Mappings.Select(mapping => new PayrollJournalMapping { Key = mapping.Key, DebitAccount = mapping.DebitAccount, CreditAccount = mapping.CreditAccount })];

        for (var i = 0; i < BlankRows; i++)
        {
            model.Mappings.Add(new PayrollJournalMapping());
        }
    }
}
