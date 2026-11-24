using System.Text.Json.Serialization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Manages the tax forms made on the Tax Forms page or by a recipe.
/// </summary>
public interface ITaxFormManager : ICatalogManager<TaxForm>
{
    /// <summary>
    /// Finds a form by its stored name, ignoring case, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The name, such as US-941.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The form.</returns>
    Task<TaxForm> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the forms a source prepares.
    /// </summary>
    /// <param name="source">The source, such as Payroll.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The forms.</returns>
    Task<IReadOnlyList<TaxForm>> ListBySourceAsync(string source, CancellationToken cancellationToken = default);
}

/// <summary>
/// The tax form sources modules add: what prepares a form and supplies the functions its formulas read records with.
/// </summary>
public sealed class TaxFormSourceOptions
{
    /// <summary>Gets the sources, in the order they were added.</summary>
    public List<TaxFormSource> Sources { get; } = [];
}

/// <summary>
/// A tax form source, such as Payroll's pay statements.
/// </summary>
/// <param name="Name">The stored name, such as Payroll.</param>
/// <param name="DisplayName">What people see.</param>
/// <param name="Description">What it prepares forms from.</param>
/// <param name="ReportedAmountsLabel">What the records that report amounts to a field are called, such as pay codes; <see langword="null"/> when the source has none.</param>
public sealed record TaxFormSource(
    string Name,
    [property: JsonIgnore] LocalizationSource DisplayName,
    [property: JsonIgnore] LocalizationSource Description,
    [property: JsonIgnore] LocalizationSource ReportedAmountsLabel = null);

/// <summary>
/// Tells what uses a tax form, such as the filings a module prepared from it: a used form keeps its name (filings keep
/// it) and is turned off rather than deleted. Register one with <c>services.AddTaxFormUsage&lt;T&gt;()</c>.
/// </summary>
public interface ITaxFormUsage
{
    /// <summary>
    /// Gets what uses a form, in words people read, such as "3 filings of Contoso".
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The uses; empty when nothing uses it.</returns>
    Task<IReadOnlyList<string>> GetUsagesAsync(TaxForm form, CancellationToken cancellationToken = default);
}
