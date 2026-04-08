using System.Security.Claims;
using CrestApps.Core.AI.Copilot.Services;
using CrestApps.Core.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Controllers;

[Area("AIChat")]
[Authorize(Policy = "Admin")]
public sealed class CopilotAuthController : Controller
{
    private readonly GitHubOAuthService _oauthService;
    private readonly ILogger<CopilotAuthController> _logger;

    public CopilotAuthController(
        GitHubOAuthService oauthService,
        ILogger<CopilotAuthController> logger)
    {
        _oauthService = oauthService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Authorize(string returnUrl = null)
    {
        var safeReturnUrl = string.Equals(returnUrl, "__popup__", StringComparison.Ordinal)
        ? "__popup__"
        : returnUrl != null && Url.IsLocalUrl(returnUrl)
        ? returnUrl
        : Url.Action("Index", "Settings", new { area = "Admin" });

        var callbackUrl = Url.Action("OAuthCallback", "CopilotAuth", new { area = "AIChat" }, Request.Scheme);

        var authUrl = _oauthService.GetAuthorizationUrl(callbackUrl, safeReturnUrl);

        return Redirect(authUrl);
    }

    [HttpGet]
    public async Task<IActionResult> OAuthCallback(string code, string state, string error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("GitHub OAuth error: {Error}", error.SanitizeLogValue());
            TempData["ErrorMessage"] = "GitHub authentication failed. Please try again.";

            return HandleOAuthReturn(state, success: false, username: null);
        }

        if (string.IsNullOrEmpty(code))
        {
            _logger.LogWarning("No authorization code received from GitHub.");
            TempData["ErrorMessage"] = "No authorization code received from GitHub.";

            return HandleOAuthReturn(state, success: false, username: null);
        }

        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var credential = await _oauthService.ExchangeCodeForTokenAsync(code, userId);

            TempData["SuccessMessage"] = $"Successfully connected to GitHub as {credential.GitHubUsername}.";

            return HandleOAuthReturn(state, success: true, username: credential.GitHubUsername);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange GitHub authorization code for tokens.");
            TempData["ErrorMessage"] = "Failed to connect to GitHub. Please try again.";
        }

        return HandleOAuthReturn(state, success: false, username: null);
    }

    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var isAuthenticated = await _oauthService.IsAuthenticatedAsync(userId);
        string gitHubUsername = null;

        if (isAuthenticated)
        {
            var credential = await _oauthService.GetCredentialAsync(userId);
            gitHubUsername = credential?.GitHubUsername;
        }

        return Json(new { isAuthenticated, gitHubUsername });
    }

    [HttpGet]
    public async Task<IActionResult> Models()
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var models = await _oauthService.ListModelsAsync(userId);

        return Json(models.Select(m => new { m.Id, m.Name }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect(string returnUrl = null)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        await _oauthService.DisconnectAsync(userId);

        TempData["SuccessMessage"] = "Successfully disconnected from GitHub.";

        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Settings", new { area = "Admin" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisconnectAjax()
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        await _oauthService.DisconnectAsync(userId);

        return Json(new { success = true });
    }

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

        if (Url.IsLocalUrl(state))
        {
            return Redirect(state);
        }

        return RedirectToAction("Index", "Settings", new { area = "Admin" });
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.Name);
}
