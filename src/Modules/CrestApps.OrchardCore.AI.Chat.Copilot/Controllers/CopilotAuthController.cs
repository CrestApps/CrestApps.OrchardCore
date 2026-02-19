using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using USR = OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Controllers;

[Authorize]
public sealed class CopilotAuthController : Controller
{
    private readonly GitHubOAuthService _oauthService;
    private readonly UserManager<USR.IUser> _userManager;
    private readonly INotifier _notifier;
    private readonly ILogger _logger;
    private readonly AdminOptions _adminOptions;

    internal readonly IHtmlLocalizer H;

    public CopilotAuthController(
        GitHubOAuthService oauthService,
        UserManager<USR.IUser> userManager,
        INotifier notifier,
        IHtmlLocalizer<CopilotAuthController> htmlLocalizer,
        ILogger<CopilotAuthController> logger,
        IOptions<AdminOptions> adminOptions)
    {
        _oauthService = oauthService;
        _userManager = userManager;
        _notifier = notifier;
        _logger = logger;
        _adminOptions = adminOptions.Value;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Initiates the GitHub OAuth flow.
    /// </summary>
    [HttpGet("copilot/Authorize")]
    public async Task<IActionResult> AuthorizeGitHub(string returnUrl = null)
    {
        // Validate returnUrl to prevent open redirect attacks.
        // Fallback to admin home — never to OAuthCallback itself (which would trigger a loop).
        // Special case: "__popup__" is a sentinel value for popup-based auth flows.
        var safeReturnUrl = string.Equals(returnUrl, "__popup__", StringComparison.Ordinal)
            ? "__popup__"
            : returnUrl != null && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : "~/" + _adminOptions.AdminUrlPrefix;

        // Generate the GitHub authorization URL
        var authUrl = await _oauthService.GetAuthorizationUrlAsync(safeReturnUrl);

        return Redirect(authUrl);
    }

    /// <summary>
    /// Handles the OAuth callback from GitHub.
    /// </summary>
    [HttpGet("copilot/OAuthCallback")]
    public async Task<IActionResult> OAuthCallback(string code, string state, string error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("GitHub OAuth error: {Error}", error);
            await _notifier.ErrorAsync(H["GitHub authentication failed: {0}", error]);

            return HandleOAuthReturn(state, success: false, username: null);
        }

        if (string.IsNullOrEmpty(code))
        {
            _logger.LogWarning("No authorization code received from GitHub");
            await _notifier.ErrorAsync(H["No authorization code received from GitHub"]);

            return HandleOAuthReturn(state, success: false, username: null);
        }

        // Get current user
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("No authenticated user found during OAuth callback");

            return Unauthorized();
        }

        try
        {
            // Exchange code for tokens and store on user
            var credential = await _oauthService.ExchangeCodeForTokenAsync(code, await _userManager.GetUserIdAsync(user));

            await _notifier.SuccessAsync(H["Successfully connected to GitHub as {0}", credential.GitHubUsername]);

            return HandleOAuthReturn(state, success: true, username: credential.GitHubUsername);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange GitHub authorization code for tokens");
            await _notifier.ErrorAsync(H["Failed to connect to GitHub. Please try again."]);
        }

        return HandleOAuthReturn(state, success: false, username: null);
    }

    /// <summary>
    /// Routes the OAuth callback to a popup close page or a standard redirect.
    /// When the returnUrl/state is "__popup__", the callback was initiated from a popup window
    /// (e.g., the AI Profile edit form), so we render a small page that sends the result back
    /// to the opener via <c>postMessage</c> and closes itself.
    /// </summary>
    private IActionResult HandleOAuthReturn(string state, bool success, string username)
    {
        if (string.Equals(state, "__popup__", StringComparison.Ordinal))
        {
            var safeUsername = System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(username ?? string.Empty);

            return Content(
                "<!DOCTYPE html><html><body><script>" +
                $"window.opener.postMessage({{ type: 'github-auth-complete', success: {(success ? "true" : "false")}, username: '{safeUsername}' }}, window.location.origin);" +
                "window.close();" +
                "</script></body></html>",
                "text/html");
        }

        return RedirectToLocal(state);
    }

    /// <summary>
    /// Disconnects the user's GitHub account.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisconnectGitHub(string returnUrl = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        await _oauthService.DisconnectAsync(await _userManager.GetUserIdAsync(user));

        await _notifier.SuccessAsync(H["Successfully disconnected from GitHub"]);

        return RedirectToLocal(returnUrl);
    }

    private IActionResult RedirectToLocal(string returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return LocalRedirect("~/" + _adminOptions.AdminUrlPrefix);
    }
}
