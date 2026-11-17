using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Core.Canada;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.Canada;

/// <summary>
/// An employee of an employer registered with the CRA needs exactly one province of employment, from their work site:
/// their provincial tax, CPP or QPP, and the T4's box 10 go by it, never by where they live.
/// </summary>
public sealed class CanadaProvinceOfEmploymentBlockers : IPayProfileBlockerProvider
{
    /// <summary>The blocker's code with no province of employment.</summary>
    public const string NoProvinceCode = "NoProvinceOfEmployment";

    /// <summary>The blocker's code with several.</summary>
    public const string SeveralProvincesCode = "SeveralProvincesOfEmployment";

    private readonly IEmployerManager _employers;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CanadaProvinceOfEmploymentBlockers"/> class.
    /// </summary>
    /// <param name="employers">The employers.</param>
    /// <param name="services">The services, for the work site.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CanadaProvinceOfEmploymentBlockers(IEmployerManager employers, IServiceProvider services, IStringLocalizer<CanadaProvinceOfEmploymentBlockers> stringLocalizer)
    {
        _employers = employers;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task AddBlockersAsync(EmployeePayProfile profile, IList<PayBlocker> blockers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(blockers);

        if (profile.WorkerType != PayWorkerType.Employee || string.IsNullOrEmpty(profile.EmployerId))
        {
            return;
        }

        var employer = await _employers.FindByIdAsync(profile.EmployerId, cancellationToken);

        if (employer?.Registrations.Any(registration => string.Equals(registration.Jurisdiction, CanadaIncomeTax.Jurisdiction, StringComparison.OrdinalIgnoreCase)) != true)
        {
            return;
        }

        var provinces = (await PayrollWorkSites.JurisdictionsAsync(_services, profile, cancellationToken))
            .Where(jurisdiction => jurisdiction.StartsWith(CanadaIncomeTax.Jurisdiction + "-", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (provinces.Count == 0)
        {
            blockers.Add(new PayBlocker(NoProvinceCode, S["Choose the person's work site, with its province on the site's Payroll card: a Canadian employee's taxes go by their province of employment, never by where they live."]));
        }
        else if (provinces.Count > 1)
        {
            blockers.Add(new PayBlocker(SeveralProvincesCode, S["The work site names several provinces ({0}): a Canadian employee has one province of employment.", string.Join(", ", provinces)]));
        }
    }
}
