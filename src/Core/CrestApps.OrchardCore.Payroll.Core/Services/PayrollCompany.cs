using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Finds the site's own company: the employer Payroll's settings name, or the oldest employer when they name none or
/// one that was deleted.
/// </summary>
internal sealed class PayrollCompanyService : IPayrollCompanyService
{
    private readonly ISiteService _siteService;
    private readonly EmployerStore _employers;

    // Payroll's core also runs without Orchard's site settings (a host of its own, the engine's tests): the company is
    // then the oldest employer.
    public PayrollCompanyService(IEnumerable<ISiteService> siteServices, EmployerStore employers)
    {
        _siteService = siteServices.FirstOrDefault();
        _employers = employers;
    }

    /// <inheritdoc />
    public async Task<Employer> GetCompanyAsync(CancellationToken cancellationToken = default)
    {
        var id = _siteService is null ? null : (await _siteService.GetSettingsAsync<PayrollSettings>())?.CompanyEmployerId;

        if (!string.IsNullOrEmpty(id) && await _employers.FindByIdAsync(id, cancellationToken) is { } company)
        {
            return company;
        }

        return (await _employers.ListAsync(cancellationToken))
            .OrderBy(employer => employer.CreatedUtc)
            .ThenBy(employer => employer.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task SetCompanyAsync(string employerId)
    {
        if (_siteService is null)
        {
            return;
        }

        var site = await _siteService.LoadSiteSettingsAsync();

        site.Alter<PayrollSettings>(settings => settings.CompanyEmployerId = string.IsNullOrEmpty(employerId) ? null : employerId);

        await _siteService.UpdateSiteSettingsAsync(site);
    }
}

/// <summary>
/// Gives a new pay run the company as its employer when nothing chose one: the company's own payroll never asks for an
/// employer. With Multiple Employers on, the employer picker shows the company chosen and may change it. Registered
/// before the handler that checks the employer.
/// </summary>
internal sealed class PayRunCompanyEmployerHandler : CatalogEntryHandlerBase<PayRun>
{
    private readonly IPayrollCompanyService _company;

    public PayRunCompanyEmployerHandler(IPayrollCompanyService company)
    {
        _company = company;
    }

    public override Task InitializedAsync(InitializedContext<PayRun> context, CancellationToken cancellationToken = default)
        => FillAsync(context.Model, cancellationToken);

    public override Task ValidatingAsync(ValidatingContext<PayRun> context, CancellationToken cancellationToken = default)
        => FillAsync(context.Model, cancellationToken);

    public override Task CreatingAsync(CreatingContext<PayRun> context, CancellationToken cancellationToken = default)
        => FillAsync(context.Model, cancellationToken);

    private async Task FillAsync(PayRun run, CancellationToken cancellationToken)
    {
        if (run is not null && string.IsNullOrEmpty(run.EmployerId))
        {
            run.EmployerId = (await _company.GetCompanyAsync(cancellationToken))?.ItemId;
        }
    }
}

/// <summary>
/// Gives a new pay profile the company as its employer when nothing chose one, like
/// <see cref="PayRunCompanyEmployerHandler"/>.
/// </summary>
internal sealed class PayProfileCompanyEmployerHandler : CatalogEntryHandlerBase<EmployeePayProfile>
{
    private readonly IPayrollCompanyService _company;

    public PayProfileCompanyEmployerHandler(IPayrollCompanyService company)
    {
        _company = company;
    }

    public override Task InitializedAsync(InitializedContext<EmployeePayProfile> context, CancellationToken cancellationToken = default)
        => FillAsync(context.Model, cancellationToken);

    public override Task ValidatingAsync(ValidatingContext<EmployeePayProfile> context, CancellationToken cancellationToken = default)
        => FillAsync(context.Model, cancellationToken);

    public override Task CreatingAsync(CreatingContext<EmployeePayProfile> context, CancellationToken cancellationToken = default)
        => FillAsync(context.Model, cancellationToken);

    private async Task FillAsync(EmployeePayProfile profile, CancellationToken cancellationToken)
    {
        if (profile is not null && string.IsNullOrEmpty(profile.EmployerId))
        {
            profile.EmployerId = (await _company.GetCompanyAsync(cancellationToken))?.ItemId;
        }
    }
}
