using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Turns a person's home address into the tax jurisdictions of their home that income tax rules match by
/// (<c>IncomeTaxRequest.HomeJurisdictions</c>): the country, the country with its region, and the local jurisdictions of
/// the address, such as US, US-MI and US-MI-DETROIT. Replace it to match local jurisdictions another way (an address
/// service, school districts).
/// </summary>
public interface IPayrollHomeJurisdictionResolver
{
    /// <summary>
    /// Gets the home jurisdictions of a pay profile on a day.
    /// </summary>
    /// <param name="profile">The pay profile, with its home address.</param>
    /// <param name="on">The day, such as the pay date.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The jurisdiction codes, upper-case, the country first; none when the profile has no home country.</returns>
    Task<IReadOnlyList<string>> ResolveAsync(EmployeePayProfile profile, DateOnly on, CancellationToken cancellationToken = default);
}
