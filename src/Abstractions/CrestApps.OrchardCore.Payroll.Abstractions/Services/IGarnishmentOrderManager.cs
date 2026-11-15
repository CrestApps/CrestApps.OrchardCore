using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Manages garnishment orders.
/// </summary>
public interface IGarnishmentOrderManager : ICatalogManager<GarnishmentOrder>
{
    /// <summary>
    /// Lists the orders of a pay profile.
    /// </summary>
    /// <param name="profileId">The pay profile.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The orders.</returns>
    Task<IReadOnlyList<GarnishmentOrder>> ListByProfileAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the orders of some employers, or of all when <paramref name="employerIds"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="employerIds">The employers, or <see langword="null"/> for every one.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The orders.</returns>
    Task<IReadOnlyList<GarnishmentOrder>> ListByEmployersAsync(IReadOnlyCollection<string> employerIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// The defaults a garnishment order's kind gives: the United States federal limits (Consumer Credit Protection Act
/// Title III, the Higher Education Act and the Internal Revenue Code), which an order may change.
/// </summary>
public static class GarnishmentDefaults
{
    /// <summary>
    /// The pay a week a creditor order leaves: 30 times the federal minimum wage of 7.25.
    /// </summary>
    public const decimal ProtectedWeeklyAmount = 217.50m;

    /// <summary>
    /// Gets the default limit, as a percent of disposable earnings, of a kind.
    /// </summary>
    /// <param name="type">The kind.</param>
    /// <returns>The percent, or <see langword="null"/> for no percent limit.</returns>
    public static decimal? LimitPercentFor(GarnishmentType type)
        => type switch
        {
            GarnishmentType.Support => 50m,
            GarnishmentType.StudentLoan => 15m,
            GarnishmentType.Creditor or GarnishmentType.StateTaxLevy or GarnishmentType.Other => 25m,
            _ => null,
        };

    /// <summary>
    /// Gets the default priority of a kind: support first, then tax levies, bankruptcy, student loans and creditors.
    /// </summary>
    /// <param name="type">The kind.</param>
    /// <returns>The priority.</returns>
    public static int PriorityFor(GarnishmentType type)
        => type switch
        {
            GarnishmentType.Support => 10,
            GarnishmentType.FederalTaxLevy => 20,
            GarnishmentType.StateTaxLevy => 30,
            GarnishmentType.Bankruptcy => 40,
            GarnishmentType.StudentLoan => 50,
            GarnishmentType.Creditor => 60,
            _ => 70,
        };

    /// <summary>
    /// Gets whether a kind leaves the protected weekly amount to the person.
    /// </summary>
    /// <param name="type">The kind.</param>
    /// <returns><see langword="true"/> for creditor-like orders.</returns>
    public static bool ProtectsWeeklyAmount(GarnishmentType type)
        => type is GarnishmentType.Creditor or GarnishmentType.StudentLoan or GarnishmentType.StateTaxLevy or GarnishmentType.Other;
}
