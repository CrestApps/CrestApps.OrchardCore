using System.Security.Claims;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services.Intraday;

/// <summary>
/// Adds the Offers page to My Work for workers, with the number of open offers they can take.
/// </summary>
internal sealed class IntradayOffersLinkProvider : IMyWorkLinkProvider
{
    private readonly IntradayService _intradayService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntradayOffersLinkProvider"/> class.
    /// </summary>
    /// <param name="intradayService">The intraday service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public IntradayOffersLinkProvider(IntradayService intradayService, IStringLocalizer<IntradayOffersLinkProvider> stringLocalizer)
    {
        _intradayService = intradayService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<MyWorkLink>> GetLinksAsync(ClaimsPrincipal user, WorkerProfile worker, CancellationToken cancellationToken = default)
    {
        if (worker is null)
        {
            return [];
        }

        var open = await _intradayService.OpenForAsync(worker, cancellationToken);

        return [new MyWorkLink("offers", S["Offers"], "~/my-work/offers", open.Count)];
    }
}
