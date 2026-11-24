using System.Security.Claims;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Staff.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Builds a person's own pay, as the Pay section of their staff profile shows it: coming pay to review, pay statements,
/// filed tax forms, and the pay details they may change.
/// </summary>
internal sealed class MyPayPageBuilder
{
    private readonly IServiceProvider _services;
    private readonly IAuthorizationService _authorizationService;

    public MyPayPageBuilder(IServiceProvider services, IAuthorizationService authorizationService)
    {
        _services = services;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Builds the pay of a staff member for the signed-in person who is that staff member.
    /// </summary>
    /// <param name="me">The staff member.</param>
    /// <param name="user">The signed-in person.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page's model.</returns>
    public async Task<MyPayIndexViewModel> BuildAsync(StaffMember me, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(me);

        var model = new MyPayIndexViewModel { IsStaff = true };

        var meId = me.ItemId;
        var draft = nameof(PayStatementStatus.Draft);
        var statements = await _services.GetRequiredService<PayStatementStore>().ListAsync(index => index.StaffMemberId == meId && index.Status != draft, cancellationToken);
        var employers = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var employerId in statements.Select(statement => statement.EmployerId).Distinct(StringComparer.Ordinal))
        {
            employers[employerId] = (await _services.GetRequiredService<EmployerStore>().FindByIdAsync(employerId, cancellationToken))?.Name ?? employerId;
        }

        model.Statements = new AdminListContext<MyPayStatementRow>
        {
            Source = [.. statements
                .OrderByDescending(statement => statement.PayDate)
                .ThenByDescending(statement => statement.Number, StringComparer.Ordinal)
                .Select(statement => new MyPayStatementRow { Statement = statement, EmployerName = employers.GetValueOrDefault(statement.EmployerId) ?? statement.Snapshot?.EmployerName })],
        };

        foreach (var profile in await _services.GetRequiredService<IPayProfileManager>().ListByStaffMemberAsync(meId, cancellationToken))
        {
            model.Profiles.Add((profile, (await _services.GetRequiredService<EmployerStore>().FindByIdAsync(profile.EmployerId, cancellationToken))?.Name ?? profile.EmployerId));
        }

        model.CanManageDetails = await _authorizationService.AuthorizeAsync(user, PayrollPermissions.ManageOwnPayDetails);

        // Coming pay to review before the run is submitted, when employees review their pay.
        if (_services.GetService<PayStatementReviewService>() is { } reviews)
        {
            foreach (var (statement, _) in await reviews.ListOpenAsync(meId, cancellationToken))
            {
                model.ToReview.Add((statement, (await _services.GetRequiredService<EmployerStore>().FindByIdAsync(statement.EmployerId, cancellationToken))?.Name ?? statement.Snapshot?.EmployerName));
            }
        }

        // The person's filed tax forms, such as wage statements.
        if (_services.GetService<IPayrollFilingManager>() is { } filings && model.Profiles.Count > 0)
        {
            var taxForms = _services.GetService<CrestApps.OrchardCore.Taxation.Services.ITaxFormManager>() is { } manager
                ? (await manager.ListBySourceAsync(PayrollConstants.TaxFormSource, cancellationToken)).Where(form => !string.IsNullOrEmpty(form.Name)).GroupBy(form => form.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase)
                : [];
            var forms = (await filings.ListByEmployersAsync([.. model.Profiles.Select(pair => pair.Profile.EmployerId).Distinct(StringComparer.Ordinal)], cancellationToken))
                .Where(filing => filing.Status == PayrollFilingStatus.Filed)
                .SelectMany(filing => filing.People.Where(person => person.StaffMemberId == meId).Select(person => new MyPayTaxFormViewModel
                {
                    Filing = filing,
                    Person = person,
                    EmployerName = model.Profiles.FirstOrDefault(pair => pair.Profile.EmployerId == filing.EmployerId).EmployerName,
                    StatementName = taxForms.TryGetValue(filing.Form ?? string.Empty, out var form)
                        ? (string.IsNullOrWhiteSpace(form.Statement?.Name) ? form.DisplayName : form.Statement.Name)
                        : filing.Form,
                }))
                .OrderByDescending(form => form.Filing.Year)
                .ToList();

            model.HasTaxForms = forms.Count > 0;
            model.TaxForms = new AdminListContext<MyPayTaxFormViewModel> { Source = forms };
        }

        model.ComingPay = new AdminListContext<MyPayComingPayRow> { Source = [.. model.ToReview.Select(pair => new MyPayComingPayRow { Statement = pair.Statement, EmployerName = pair.EmployerName })] };
        model.PayDetails = new AdminListContext<MyPayProfileRow> { Source = [.. model.Profiles.Select(pair => new MyPayProfileRow { Profile = pair.Profile, EmployerName = pair.EmployerName, CanManageDetails = model.CanManageDetails })] };

        // Banking keeps the person's own bank accounts: link to it when it is on.
        model.CanManageBankAccounts = _services.GetService<IBankAccountManager>() is not null
            && await _authorizationService.AuthorizeAsync(user, BankingPermissions.ManageOwnBankAccounts);

        return model;
    }
}
