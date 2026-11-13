using CrestApps.OrchardCore.Checks.Models;

namespace CrestApps.OrchardCore.Checks.Services;

/// <summary>
/// Supplies the voucher (stub) of the checks a module issues, such as the earnings, deductions, taxes and year-to-date
/// totals of a pay statement. Register one per source kind with
/// <see cref="ChecksServiceCollectionExtensions.AddCheckVoucherSource{TSource}"/>.
/// </summary>
public interface ICheckVoucherSource
{
    /// <summary>
    /// Gets the source kind of the checks this source describes, as on <see cref="CheckRecord.SourceKind"/>.
    /// </summary>
    string SourceKind { get; }

    /// <summary>
    /// Returns the voucher of a check.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The voucher, or <see langword="null"/> for none.</returns>
    Task<CheckVoucher> GetVoucherAsync(CheckRecord check, CancellationToken cancellationToken = default);
}
