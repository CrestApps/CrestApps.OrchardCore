using System.Security.Claims;
using CrestApps.OrchardCore.Plaid.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Plaid.Controllers;

/// <summary>
/// Checks the saved Plaid settings by asking Plaid for a link token.
/// </summary>
[Admin]
public sealed class PlaidSettingsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IPlaidCredentialsProvider _credentialsProvider;
    private readonly IPlaidLinkSite _linkSite;
    private readonly PlaidClient _client;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public PlaidSettingsController(
        IAuthorizationService authorizationService,
        IPlaidCredentialsProvider credentialsProvider,
        IPlaidLinkSite linkSite,
        PlaidClient client,
        INotifier notifier,
        IHtmlLocalizer<PlaidSettingsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _credentialsProvider = credentialsProvider;
        _linkSite = linkSite;
        _client = client;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Asks Plaid for a link token with the saved settings and says whether it worked.
    /// </summary>
    [HttpPost]
    [Admin("plaid/test", "PlaidSettingsTest")]
    public async Task<IActionResult> Test()
    {
        if (!await _authorizationService.AuthorizeAsync(User, PlaidPermissions.ManagePlaidSettings))
        {
            return Forbid();
        }

        var credentials = await _credentialsProvider.GetAsync(HttpContext.RequestAborted);

        if (credentials is null)
        {
            await _notifier.ErrorAsync(H["Plaid is not set up: enter the client id and choose a secret that exists and holds the Plaid secret."]);
        }
        else
        {
            try
            {
                var request = await _linkSite.CreateLinkTokenRequestAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "settings-test", HttpContext.RequestAborted);

                await _client.CreateLinkTokenAsync(credentials, request, HttpContext.RequestAborted);
                await _notifier.SuccessAsync(H["Plaid answered: the {0} settings work.", credentials.Environment.ToString()]);
            }
            catch (PlaidApiException exception)
            {
                await _notifier.ErrorAsync(H["Plaid refused the settings: {0} {1}. Check the client id, the environment and the secret.", exception.Error.ErrorType, exception.ErrorCode]);
            }
            catch (HttpRequestException)
            {
                await _notifier.ErrorAsync(H["Plaid could not be reached from this server."]);
            }
        }

        return RedirectToAction("Index", "Admin", new { area = "OrchardCore.Settings", groupId = PlaidConstants.SettingsGroupId });
    }
}
