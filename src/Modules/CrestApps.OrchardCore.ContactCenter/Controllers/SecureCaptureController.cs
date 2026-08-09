using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Serves the customer-facing secure data capture page. A live customer opens this page from a one-time link the
/// agent shares, enters sensitive data, and submits it directly to the tokenization boundary, so the agent, the
/// supervisor, and the recording never see the raw value. The page is anonymous by design: it is authorized only
/// by the unguessable one-time token, because the customer is not an authenticated platform user.
/// </summary>
[Feature(ContactCenterConstants.Feature.SecureCapture)]
[AllowAnonymous]
public sealed class SecureCaptureController : Controller
{
    /// <summary>
    /// The route name of the customer capture page, used to build the link the agent shares.
    /// </summary>
    public const string CaptureRouteName = "ContactCenterSecureCapture";

    private readonly ISecureCaptureService _secureCaptureService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureCaptureController"/> class.
    /// </summary>
    /// <param name="secureCaptureService">The secure capture orchestration service.</param>
    public SecureCaptureController(ISecureCaptureService secureCaptureService)
    {
        _secureCaptureService = secureCaptureService;
    }

    /// <summary>
    /// Renders the secure capture form for a valid, unexpired capture the token authorizes.
    /// </summary>
    /// <param name="token">The one-time access token from the customer link.</param>
    /// <returns>The capture form, or a not-found result when the token is invalid or expired.</returns>
    [HttpGet]
    [Route("contact-center/secure-capture/{token}", Name = CaptureRouteName)]
    public async Task<IActionResult> Index(string token)
    {
        ApplyPrivacyHeaders();

        var session = await _secureCaptureService.GetForCustomerAsync(token, HttpContext.RequestAborted);

        if (session is null)
        {
            return NotFound();
        }

        return View(new SecureCaptureFormViewModel
        {
            Token = token,
            Fields = session.RequestedFields.ToArray(),
        });
    }

    /// <summary>
    /// Tokenizes the sensitive values a customer submitted and completes the capture. The raw values reach only
    /// the tokenization boundary and are never persisted, logged, or returned.
    /// </summary>
    /// <param name="token">The one-time access token from the customer link.</param>
    /// <returns>A confirmation view on success, or the form with an error message on failure.</returns>
    [HttpPost]
    [Route("contact-center/secure-capture/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(string token)
    {
        ApplyPrivacyHeaders();

        var session = await _secureCaptureService.GetForCustomerAsync(token, HttpContext.RequestAborted);

        if (session is null)
        {
            return NotFound();
        }

        var values = new Dictionary<SecureCaptureField, string>();

        foreach (var field in session.RequestedFields)
        {
            values[field] = Request.Form[$"field_{field}"].ToString();
        }

        var result = await _secureCaptureService.SubmitAsync(token, values, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return View(nameof(Index), new SecureCaptureFormViewModel
            {
                Token = token,
                Fields = session.RequestedFields.ToArray(),
                ErrorMessage = result.Reason,
            });
        }

        return View(nameof(Index), new SecureCaptureFormViewModel
        {
            Token = token,
            Fields = session.RequestedFields.ToArray(),
            Completed = true,
        });
    }

    /// <summary>
    /// Applies response headers that keep the one-time capture link and the sensitive form off caches, out of the
    /// browser history heuristics, and away from cross-origin referrers, so the token in the URL is not leaked.
    /// </summary>
    private void ApplyPrivacyHeaders()
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
