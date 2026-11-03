using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Telnyx.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Telnyx.Controllers;

/// <summary>
/// The Telnyx account's phone numbers: list them, search the numbers for sale and buy one, set the caller name a
/// number shows, and release one.
/// </summary>
[Admin]
public sealed class TelnyxNumbersController : Controller
{
    private readonly TelnyxApiClient _client;
    private readonly TelnyxCallerNumberReputation _reputation;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxNumbersController"/> class.
    /// </summary>
    /// <param name="client">The Telnyx API client.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="reputation">The carriers' spam rejections of the account's numbers.</param>
    public TelnyxNumbersController(TelnyxApiClient client, IAuthorizationService authorizationService, INotifier notifier, IHtmlLocalizer<TelnyxNumbersController> htmlLocalizer, TelnyxCallerNumberReputation reputation)
    {
        _client = client;
        _reputation = reputation;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the account's numbers.
    /// </summary>
    /// <returns>The list.</returns>
    [Admin("telnyx/numbers", "TelnyxNumbersIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbid();
        }

        var (result, numbers) = await _client.ListOwnedNumbersAsync(1, 250, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["Telnyx did not list the numbers ({0}). Check the API key in the Telnyx settings.", (int?)result.StatusCode]);
        }

        await _reputation.ApplyToAsync(numbers);

        return View(new TelnyxNumbersViewModel { List = new AdminListContext<TelnyxOwnedNumber> { Source = numbers } });
    }

    /// <summary>
    /// Searches the numbers for sale.
    /// </summary>
    /// <param name="search">What to look for.</param>
    /// <returns>The search form and what it found.</returns>
    [Admin("telnyx/numbers/search", "TelnyxNumbersSearch")]
    public async Task<IActionResult> Search(TelnyxNumberSearchViewModel search)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbid();
        }

        search ??= new TelnyxNumberSearchViewModel();
        IReadOnlyList<TelnyxAvailableNumber> numbers = [];

        if (search.Submitted)
        {
            var (result, found) = await _client.SearchAvailableNumbersAsync(new TelnyxNumberSearch
            {
                CountryCode = search.CountryCode,
                AreaCode = search.AreaCode,
                City = search.City,
                Region = search.Region,
                Contains = search.Contains,
                Features = search.Sms ? ["voice", "sms"] : ["voice"],
                Limit = 30,
            }, HttpContext.RequestAborted);

            numbers = found;

            if (!result.Succeeded)
            {
                await _notifier.ErrorAsync(H["Telnyx did not search the numbers ({0}).", (int?)result.StatusCode]);
            }
        }

        search.List = new AdminListContext<TelnyxAvailableNumber> { Source = numbers };

        return View(search);
    }

    /// <summary>
    /// Buys a number and adds it as a channel address.
    /// </summary>
    /// <param name="phoneNumber">The number, from a search.</param>
    /// <returns>A redirect to the account's numbers.</returns>
    [HttpPost]
    [Admin("telnyx/numbers/buy", "TelnyxNumbersBuy")]
    public async Task<IActionResult> Buy(string phoneNumber)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbid();
        }

        var (result, _, status) = await _client.OrderNumbersAsync([phoneNumber], cancellationToken: HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["Telnyx did not sell {0} ({1}): {2}", phoneNumber, (int?)result.StatusCode, result.ErrorBody]);

            return RedirectToAction(nameof(Search));
        }

        if (HttpContext.RequestServices.GetService<ICatalogManager<OmnichannelChannelEndpoint>>() is { } endpoints)
        {
            var endpoint = await endpoints.NewAsync(cancellationToken: HttpContext.RequestAborted);
            endpoint.DisplayText = phoneNumber;
            endpoint.Value = phoneNumber;
            endpoint.Channel = OmnichannelConstants.Channels.Phone;
            endpoint.Capabilities = [OmnichannelConstants.Channels.Phone];
            endpoint.ProviderName = TelnyxConstants.ProviderTechnicalName;

            if ((await endpoints.ValidateAsync(endpoint, HttpContext.RequestAborted)).Succeeded)
            {
                await endpoints.CreateAsync(endpoint, HttpContext.RequestAborted);
            }
        }

        await _notifier.SuccessAsync(H["{0} was ordered ({1}) and added as a phone address.", phoneNumber, status]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Sets the caller name a number shows.
    /// </summary>
    /// <param name="id">The number's Telnyx id.</param>
    /// <param name="callerName">The name.</param>
    /// <returns>A redirect to the account's numbers.</returns>
    [HttpPost]
    [Admin("telnyx/numbers/{id}/caller-name", "TelnyxNumbersCallerName")]
    public async Task<IActionResult> CallerName(string id, string callerName)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbid();
        }

        var result = await _client.SetCallerNameAsync(id, callerName, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The caller name was saved as \"{0}\". Carriers can take a few days to show it.", TelnyxApiClient.NormalizeCallerName(callerName)]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Telnyx did not save the caller name ({0}): {1}", (int?)result.StatusCode, result.ErrorBody]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Releases a number.
    /// </summary>
    /// <param name="id">The number's Telnyx id.</param>
    /// <returns>A redirect to the account's numbers.</returns>
    [HttpPost]
    [Admin("telnyx/numbers/{id}/release", "TelnyxNumbersRelease")]
    public async Task<IActionResult> Release(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbid();
        }

        var result = await _client.ReleaseNumberAsync(id, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The number was released."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Telnyx did not release the number ({0}): {1}", (int?)result.StatusCode, result.ErrorBody]);
        }

        return RedirectToAction(nameof(Index));
    }
}
