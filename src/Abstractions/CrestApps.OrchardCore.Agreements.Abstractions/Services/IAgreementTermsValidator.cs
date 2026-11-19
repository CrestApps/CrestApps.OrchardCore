using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Agreements.Models;

namespace CrestApps.OrchardCore.Agreements.Services;

/// <summary>
/// Checks the terms a module keeps on an agreement (in its <c>Properties</c>), such as Work Orders' visit schedules.
/// The agreement's own rules call every validator, so the editor and recipes check the same set whichever feature
/// carries the terms.
/// </summary>
public interface IAgreementTermsValidator
{
    /// <summary>
    /// Checks an agreement's terms.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What is wrong, or nothing.</returns>
    Task<IReadOnlyList<ValidationResult>> ValidateAsync(Agreement agreement, CancellationToken cancellationToken = default);
}
