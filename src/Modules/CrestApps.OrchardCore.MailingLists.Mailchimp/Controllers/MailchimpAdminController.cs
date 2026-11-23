using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Controllers;

/// <summary>
/// The Mailchimp actions of the settings page and of a list's Mailchimp card: test the connection, create the coupon
/// merge field, and register the list's webhook. Each runs on the saved settings and comes back to its page.
/// </summary>
[Admin]
public sealed class MailchimpAdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IMailchimpCredentialsProvider _credentialsProvider;
    private readonly MailchimpClient _client;
    private readonly IMailingListManager _listManager;
    private readonly SiteUrlGenerator _siteUrlGenerator;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IClock _clock;
    private readonly INotifier _notifier;
    private readonly ILogger _logger;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailchimpAdminController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="credentialsProvider">The tenant's Mailchimp credentials.</param>
    /// <param name="client">The Mailchimp client.</param>
    /// <param name="listManager">The mailing list manager.</param>
    /// <param name="siteUrlGenerator">Builds the webhook's absolute URL from the site's base URL.</param>
    /// <param name="dataProtectionProvider">Protects the webhook's secrets.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public MailchimpAdminController(
        IAuthorizationService authorizationService,
        IMailchimpCredentialsProvider credentialsProvider,
        MailchimpClient client,
        IMailingListManager listManager,
        SiteUrlGenerator siteUrlGenerator,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        INotifier notifier,
        ILogger<MailchimpAdminController> logger,
        IHtmlLocalizer<MailchimpAdminController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _credentialsProvider = credentialsProvider;
        _client = client;
        _listManager = listManager;
        _siteUrlGenerator = siteUrlGenerator;
        _dataProtectionProvider = dataProtectionProvider;
        _clock = clock;
        _notifier = notifier;
        _logger = logger;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Asks Mailchimp to answer with the saved settings (<c>GET /ping</c>) and says whether it did.
    /// </summary>
    [HttpPost]
    [Admin("mailing-lists/mailchimp/test", "MailchimpTestConnection")]
    public async Task<IActionResult> TestConnection()
    {
        if (!await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ManageMailingListSettings))
        {
            return Forbid();
        }

        var credentials = await _credentialsProvider.GetAsync(HttpContext.RequestAborted);

        if (credentials is null)
        {
            await _notifier.ErrorAsync(H["Mailchimp is not set up: choose a secret that exists and holds the Mailchimp API key."]);
        }
        else
        {
            try
            {
                await _client.PingAsync(credentials, HttpContext.RequestAborted);
                await _notifier.SuccessAsync(H["Mailchimp answered at {0}: the settings work.", credentials.BaseAddress.Host]);
            }
            catch (MailchimpApiException exception)
            {
                await _notifier.ErrorAsync(H["Mailchimp refused the API key ({0} {1}). Check the secret and the server prefix.", (int)exception.StatusCode, exception.Title ?? exception.StatusCode.ToString()]);
            }
            catch (Exception exception) when (IsUnreachable(exception))
            {
                _logger.LogWarning(exception, "The Mailchimp connection test could not reach Mailchimp.");

                await _notifier.ErrorAsync(H["Mailchimp could not be reached from this server at {0}.", credentials.BaseAddress.Host]);
            }
        }

        return RedirectToAction("Index", "Admin", new { area = "OrchardCore.Settings", groupId = MailingListsConstants.SettingsGroupId });
    }

    /// <summary>
    /// Adds the list's coupon merge field (a text field) to its audience, unless the audience has it already.
    /// </summary>
    /// <param name="listId">The list's id.</param>
    /// <param name="returnUrl">The page to come back to.</param>
    [HttpPost]
    [Admin("mailing-lists/mailchimp/{listId}/merge-field", "MailchimpCreateMergeField")]
    public async Task<IActionResult> CreateMergeField(string listId, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ManageMailingLists))
        {
            return Forbid();
        }

        var (list, settings, credentials) = await GetListAsync(listId);

        if (list is null)
        {
            return RedirectBack(listId, returnUrl);
        }

        var tag = string.IsNullOrWhiteSpace(settings.CouponMergeTag) ? MailchimpConstants.DefaultCouponMergeTag : settings.CouponMergeTag;

        if (!MailchimpListSettingsExtensions.IsValidMergeTag(tag))
        {
            await _notifier.ErrorAsync(H["The coupon merge tag {0} is not a valid Mailchimp merge tag. Correct it and save the list first.", tag]);

            return RedirectBack(listId, returnUrl);
        }

        try
        {
            var tags = await _client.GetMergeFieldTagsAsync(credentials, settings.AudienceId, HttpContext.RequestAborted);

            if (tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                await _notifier.InformationAsync(H["The audience already has the {0} merge field.", tag]);
            }
            else
            {
                await _client.CreateTextMergeFieldAsync(credentials, settings.AudienceId, tag, "Welcome coupon", HttpContext.RequestAborted);
                await _notifier.SuccessAsync(H["The {0} merge field was added to the audience.", tag]);
            }
        }
        catch (MailchimpApiException exception)
        {
            await _notifier.ErrorAsync(H["Mailchimp refused to add the merge field ({0} {1}). {2}", (int)exception.StatusCode, exception.Title ?? exception.StatusCode.ToString(), exception.Detail ?? string.Empty]);
        }
        catch (Exception exception) when (IsUnreachable(exception))
        {
            _logger.LogWarning(exception, "Mailchimp could not be reached to add a merge field.");

            await _notifier.ErrorAsync(H["Mailchimp could not be reached from this server."]);
        }

        return RedirectBack(listId, returnUrl);
    }

    /// <summary>
    /// Registers the list's webhook on its audience, at <c>mailing-lists/mailchimp/webhooks/{listId}/{secret}</c> with a
    /// new random secret, after deleting the webhook registered before. Keeps the webhook's id and its secrets,
    /// protected with the tenant's data protection keys.
    /// </summary>
    /// <param name="listId">The list's id.</param>
    /// <param name="returnUrl">The page to come back to.</param>
    [HttpPost]
    [Admin("mailing-lists/mailchimp/{listId}/webhook", "MailchimpRegisterWebhook")]
    public async Task<IActionResult> RegisterWebhook(string listId, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ManageMailingLists))
        {
            return Forbid();
        }

        var (list, settings, credentials) = await GetListAsync(listId);

        if (list is null)
        {
            return RedirectBack(listId, returnUrl);
        }

        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var url = await _siteUrlGenerator.GetUrlAsync(MailchimpConstants.WebhookRouteName, new { listId = list.ItemId, secret });

        if (!Uri.TryCreate(url, UriKind.Absolute, out var webhookUrl))
        {
            await _notifier.ErrorAsync(H["The webhook address could not be built. Set the site's base URL under Settings > General."]);

            return RedirectBack(listId, returnUrl);
        }

        try
        {
            if (!string.IsNullOrEmpty(settings.WebhookId))
            {
                await _client.DeleteWebhookAsync(credentials, settings.AudienceId, settings.WebhookId, HttpContext.RequestAborted);

                // The old webhook is gone at Mailchimp; forget it now, so a failed registration below leaves nothing stale.
                settings.ClearWebhook();
                list.Put(settings);
                await _listManager.UpdateAsync(list);
            }

            var registration = await _client.CreateWebhookAsync(credentials, settings.AudienceId, webhookUrl, HttpContext.RequestAborted);

            settings.WebhookId = registration.Id;
            settings.WebhookUrlSecret = _dataProtectionProvider.ProtectSetting(MailchimpConstants.WebhookSecretPurpose, secret);
            settings.WebhookSigningSecret = _dataProtectionProvider.ProtectSetting(MailchimpConstants.WebhookSecretPurpose, registration.SigningSecret);
            settings.WebhookRegisteredUtc = _clock.UtcNow;
            list.Put(settings);
            await _listManager.UpdateAsync(list);

            if (string.IsNullOrEmpty(registration.SigningSecret))
            {
                await _notifier.SuccessAsync(H["The webhook is registered. Mailchimp returned no signing secret, so requests are checked by the secret in the webhook's address."]);
            }
            else
            {
                await _notifier.SuccessAsync(H["The webhook is registered, and the site checks the signature of each of its requests."]);
            }
        }
        catch (MailchimpApiException exception)
        {
            await _notifier.ErrorAsync(H["Mailchimp refused the webhook ({0} {1}). {2} Mailchimp must reach {3} from the internet.", (int)exception.StatusCode, exception.Title ?? exception.StatusCode.ToString(), exception.Detail ?? string.Empty, webhookUrl.Host]);
        }
        catch (Exception exception) when (IsUnreachable(exception))
        {
            _logger.LogWarning(exception, "Mailchimp could not be reached to register a webhook.");

            await _notifier.ErrorAsync(H["Mailchimp could not be reached from this server."]);
        }

        return RedirectBack(listId, returnUrl);
    }

    private async Task<(MailingList List, MailchimpListSettings Settings, MailchimpCredentials Credentials)> GetListAsync(string listId)
    {
        var list = string.IsNullOrEmpty(listId) ? null : await _listManager.FindByIdAsync(listId, HttpContext.RequestAborted);

        if (list is null || !list.IsMailchimpList())
        {
            await _notifier.ErrorAsync(H["Save the list with Mailchimp as its provider first."]);

            return default;
        }

        var settings = list.GetMailchimpSettings();

        if (string.IsNullOrWhiteSpace(settings.AudienceId))
        {
            await _notifier.ErrorAsync(H["Choose the list's Mailchimp audience and save the list first."]);

            return default;
        }

        var credentials = await _credentialsProvider.GetAsync(HttpContext.RequestAborted);

        if (credentials is null)
        {
            await _notifier.ErrorAsync(H["Mailchimp is not set up: choose the secret that holds the API key under Settings > Mailing Lists."]);

            return default;
        }

        return (list, settings, credentials);
    }

    private IActionResult RedirectBack(string listId, string returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Edit", "MailingLists", new { area = MailingListsConstants.Features.MailingLists, id = listId });
    }

    private bool IsUnreachable(Exception exception)
        => exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException
            || (exception is OperationCanceledException && !HttpContext.RequestAborted.IsCancellationRequested);
}
