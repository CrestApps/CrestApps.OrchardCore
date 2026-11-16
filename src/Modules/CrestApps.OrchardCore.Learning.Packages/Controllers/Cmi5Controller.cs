using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Xapi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Packages.Controllers;

/// <summary>
/// The learner's side of a cmi5 package. <c>Launch</c> is called by the player page with the learner's session and
/// antiforgery token. <c>Fetch</c> is the fetch URL the AU posts to, from any origin (a relative AU runs in the
/// sandboxed frame, whose origin is opaque): only the fetch token in the path opens it, once. <c>Return</c> is the page
/// an AU returns to when it ends; in the frame it tells the player page, and in a window of its own it closes.
/// </summary>
[Feature(PackagesConstants.Features.Cmi5)]
public sealed class Cmi5Controller : Controller
{
    private readonly Cmi5LaunchService _launches;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="Cmi5Controller"/> class.
    /// </summary>
    public Cmi5Controller(
        Cmi5LaunchService launches,
        IAuthorizationService authorizationService,
        IStringLocalizer<Cmi5Controller> stringLocalizer)
    {
        _launches = launches;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Launches an AU of a cmi5 package for the signed-in learner.
    /// </summary>
    /// <param name="itemId">The package item.</param>
    /// <param name="au">The position of the AU in the course.</param>
    /// <returns>The launch URL and whether it opens in a window of its own, or why it was refused.</returns>
    [HttpPost("learning/packages/{itemId}/cmi5/launch", Name = "LearningCmi5Launch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Launch(string itemId, int au)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.AccessLearning))
        {
            return Forbid();
        }

        var origin = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        var result = await _launches.LaunchAsync(new Cmi5LaunchRequest
        {
            UserId = userId,
            UserName = User.Identity?.Name,
            PackageItemId = itemId,
            AuIndex = au,
            EndpointUrl = origin + "/" + XapiConstants.RoutePrefix + "/",
            FetchUrlBase = origin + "/learning/cmi5/fetch/",
            ReturnUrl = origin + "/learning/cmi5/return",
            RunPathBase = Request.PathBase + "/learning/packages/run/",
            Language = CultureInfo.CurrentUICulture.Name,
        }, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return Json(new { launched = false, message = ErrorText(result.Error) });
        }

        return Json(new
        {
            launched = true,
            launchUrl = result.LaunchUrl,
            ownWindow = result.OwnWindow,
            review = result.Review,
        });
    }

    /// <summary>
    /// Answers the fetch URL of a cmi5 session: the auth token the first time, and the cmi5 error after.
    /// </summary>
    /// <param name="token">The fetch token.</param>
    /// <returns><c>{"auth-token": ...}</c>, or <c>{"error-code": ..., "error-text": ...}</c>.</returns>
    [HttpPost("learning/cmi5/fetch/{token}", Name = "LearningCmi5Fetch")]
    [IgnoreAntiforgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> Fetch(string token)
    {
        SetFetchHeaders(Response);

        var result = await _launches.FetchAsync(token, HttpContext.RequestAborted);

        return result.AuthToken is not null
            ? Json(new Dictionary<string, string> { ["auth-token"] = result.AuthToken })
            : Json(new Dictionary<string, string> { ["error-code"] = result.ErrorCode, ["error-text"] = result.ErrorText });
    }

    /// <summary>
    /// Answers a CORS preflight of the fetch URL.
    /// </summary>
    /// <returns>No content.</returns>
    [HttpOptions("learning/cmi5/fetch/{token}")]
    [IgnoreAntiforgeryToken]
    [AllowAnonymous]
    public IActionResult FetchPreflight()
    {
        SetFetchHeaders(Response);
        Response.Headers.AccessControlAllowMethods = "POST, OPTIONS";
        Response.Headers.AccessControlAllowHeaders = "Content-Type";
        Response.Headers.AccessControlMaxAge = "600";

        return NoContent();
    }

    /// <summary>
    /// The page an AU returns to: in the sandboxed frame it tells the player page the AU ended, and in a window of its
    /// own it closes the window. It runs on an opaque origin, as package files do.
    /// </summary>
    /// <returns>The page.</returns>
    [HttpGet("learning/cmi5/return", Name = "LearningCmi5Return")]
    [AllowAnonymous]
    public IActionResult Return()
    {
        PackageRunController.SetSandboxHeaders(Response);
        Response.Headers.CacheControl = "no-store";

        var text = S["The lesson has ended. You can close this window."].Value;
        var page = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + System.Net.WebUtility.HtmlEncode(text) + "</title></head><body><p>" +
            System.Net.WebUtility.HtmlEncode(text) +
            "</p><script>try{if(window.parent&&window.parent!==window){window.parent.postMessage({protocol:'crestapps-cmi5',type:'return'},'*');}else if(window.opener){window.close();}}catch(e){}</script></body></html>";

        return Content(page, "text/html; charset=utf-8");
    }

    private static void SetFetchHeaders(HttpResponse response)
    {
        // The AU in the sandboxed frame has an opaque origin; the token in the path, never a cookie, opens the URL.
        response.Headers.AccessControlAllowOrigin = "*";
        response.Headers.CacheControl = "no-store";
        response.Headers.XContentTypeOptions = "nosniff";
    }

    private string ErrorText(string error)
        => error switch
        {
            PackageRuntimeErrors.NotEnrolled => S["You need an enrollment in the course to open this lesson."],
            PackageRuntimeErrors.NotActive => S["Your enrollment is not active, so the lesson cannot be opened."],
            PackageRuntimeErrors.AccessEnded => S["Your access to this course has ended."],
            PackageRuntimeErrors.NotAvailableYet => S["This lesson is not open to you yet."],
            PackageRuntimeErrors.NoPackageFile => S["No package was imported into this item yet."],
            PackageRuntimeErrors.NotAPackage or Cmi5LaunchErrors.NotCmi5 => S["This is not a cmi5 course of your course."],
            Cmi5LaunchErrors.NoSuchAu => S["The course has no such lesson."],
            PackageRuntimeErrors.Busy => S["Your progress is being saved. Try again in a moment."],
            _ => S["The lesson could not be opened."],
        };
}
