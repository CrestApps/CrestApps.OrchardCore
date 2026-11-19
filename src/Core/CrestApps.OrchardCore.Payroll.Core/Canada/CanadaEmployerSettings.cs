using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Core.Canada;

/// <summary>
/// An employer's answers the Canadian slips need that no pay statement holds, stored on the employer.
/// </summary>
public sealed class CanadaEmployerSettings
{
    /// <summary>The lowest dental benefits code.</summary>
    public const int FirstDentalBenefitsCode = 1;

    /// <summary>The highest dental benefits code.</summary>
    public const int LastDentalBenefitsCode = 5;

    /// <summary>
    /// Gets or sets the T4's box 45, the employer-offered dental benefits the employees could have had on December 31:
    /// 1 none, 2 the employee only, 3 the employee, a spouse and dependent children, 4 the employee and a spouse, 5 the
    /// employee and dependent children. Required on every T4 slip.
    /// </summary>
    public int? DentalBenefitsCode { get; set; }

    /// <summary>
    /// Gets whether a code is one of the CRA's.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsDentalBenefitsCode(int? code)
        => code is >= FirstDentalBenefitsCode and <= LastDentalBenefitsCode;
}

/// <summary>
/// Reads an employer's <see cref="CanadaEmployerSettings"/>.
/// </summary>
public static class CanadaEmployerSettingsExtensions
{
    /// <summary>
    /// Gets an employer's Canada settings, or empty ones.
    /// </summary>
    /// <param name="employer">The employer.</param>
    /// <returns>The settings.</returns>
    public static CanadaEmployerSettings GetCanadaSettings(this Employer employer)
        => employer is not null && employer.TryGet<CanadaEmployerSettings>(out var settings) && settings is not null ? settings : new CanadaEmployerSettings();
}
