using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using USR = OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Controllers;

[Authorize]
public class CopilotAuthController : Controller
{
    private readonly IGitHubOAuthService _oauthService;
    private readonly UserManager<USR.IUser> _userManager;
    private readonly ILogger<CopilotAuthController> _logger;

    public CopilotAuthController(
        IGitHubOAuthService oauthService,
        UserManager<USR.IUser> userManager,
        ILogger<CopilotAuthController> logger)
    {
        _oauthService = oauthService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Initiates the GitHub OAuth flow.
    /// </summary>
    [HttpGet]
    public IActionResult AuthorizeGitHub(string returnUrl = null)
    {
        try
        {
            // Validate returnUrl to prevent open redirect attacks
            var safeReturnUrl = returnUrl != null && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : Url.Action("OAuthCallback", "CopilotAuth");

            // Generate the GitHub authorization URL
            var authUrl = _oauthService.GetAuthorizationUrl(safeReturnUrl);

            return Redirect(authUrl);
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("GitHub OAuth is not configured. Redirecting back.");
            TempData["Error"] = "GitHub OAuth is not yet configured. Please configure GitHub OAuth App credentials.";
            return RedirectToLocal(returnUrl);
        }
    }

    /// <summary>
    /// Handles the OAuth callback from GitHub.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> OAuthCallback(string code, string state, string error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("GitHub OAuth error: {Error}", error);
            TempData["Error"] = $"GitHub authentication failed: {error}";
            return RedirectToAction("Index", "Admin", new { area = "OrchardCore.Admin" });
        }

        if (string.IsNullOrEmpty(code))
        {
            _logger.LogWarning("No authorization code received from GitHub");
            TempData["Error"] = "No authorization code received from GitHub";
            return RedirectToAction("Index", "Admin", new { area = "OrchardCore.Admin" });
        }

        try
        {
            // Get current user
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("No authenticated user found during OAuth callback");
                return Unauthorized();
            }

            // Exchange code for tokens
            var credential = await _oauthService.ExchangeCodeForTokenAsync(code, await _userManager.GetUserIdAsync(user));

            TempData["Success"] = $"Successfully connected to GitHub as {credential.GitHubUsername}";
            return RedirectToAction("Index", "Admin", new { area = "OrchardCore.Admin" });
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("GitHub OAuth token exchange is not implemented");
            TempData["Error"] = "GitHub OAuth is not yet fully implemented.";
            return RedirectToAction("Index", "Admin", new { area = "OrchardCore.Admin" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during GitHub OAuth callback");
            TempData["Error"] = "An error occurred during GitHub authentication.";
            return RedirectToAction("Index", "Admin", new { area = "OrchardCore.Admin" });
        }
    }

    /// <summary>
    /// Disconnects the user's GitHub account.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisconnectGitHub(string returnUrl = null)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            await _oauthService.DisconnectAsync(await _userManager.GetUserIdAsync(user));

            TempData["Success"] = "Successfully disconnected from GitHub";
            return RedirectToLocal(returnUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting GitHub account");
            TempData["Error"] = "An error occurred while disconnecting from GitHub.";
            return RedirectToLocal(returnUrl);
        }
    }

    private IActionResult RedirectToLocal(string returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Admin", new { area = "OrchardCore.Admin" });
    }
}
