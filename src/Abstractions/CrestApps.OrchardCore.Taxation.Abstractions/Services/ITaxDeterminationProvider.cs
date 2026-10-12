using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Provides an external tax determination (for example an integration with a third-party tax engine).
/// When an enabled provider can handle a context it short-circuits the built-in determination, keeping
/// the core engine provider-agnostic.
/// </summary>
public interface ITaxDeterminationProvider
{
    /// <summary>
    /// Gets the priority of the provider. Lower values are evaluated first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Determines whether the provider can handle the supplied context.
    /// </summary>
    /// <param name="context">The tax calculation context.</param>
    /// <returns><see langword="true"/> when the provider can determine tax for the context.</returns>
    bool CanHandle(TaxCalculationContext context);

    /// <summary>
    /// Determines the tax for the supplied context.
    /// </summary>
    /// <param name="context">The tax calculation context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The determined <see cref="TaxCalculationResult"/>.</returns>
    Task<TaxCalculationResult> DetermineAsync(TaxCalculationContext context, CancellationToken cancellationToken = default);
}
