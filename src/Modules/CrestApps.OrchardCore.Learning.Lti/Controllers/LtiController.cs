using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Lti.Controllers;

/// <summary>
/// The launch of LTI 1.3 tools, with the site as the platform: the start of the third-party initiated login, the
/// authorization endpoint the tool's login request comes back to, and the platform's key set.
/// </summary>
public sealed class LtiController : Controller
{
    private readonly IContentManager _contentManager;
    private readonly IExternalToolStore _tools;
    private readonly ICourseAccessService _courseAccess;
    private readonly ICourseStructureService _courseStructure;
    private readonly IAuthorizationService _authorizationService;
    private readonly LtiMessageHints _hints;
    private readonly ILtiPlatform _platform;
    private readonly LtiAuthorizationService _authorization;
    private readonly LtiPlatformKeys _keys;

    internal readonly IStringLocalizer S;

    public LtiController(
        IContentManager contentManager,
        IExternalToolStore tools,
        ICourseAccessService courseAccess,
        ICourseStructureService courseStructure,
        IAuthorizationService authorizationService,
        LtiMessageHints hints,
        ILtiPlatform platform,
        LtiAuthorizationService authorization,
        LtiPlatformKeys keys,
        IStringLocalizer<LtiController> stringLocalizer)
    {
        _contentManager = contentManager;
        _tools = tools;
        _courseAccess = courseAccess;
        _courseStructure = courseStructure;
        _authorizationService = authorizationService;
        _hints = hints;
        _platform = platform;
        _authorization = authorization;
        _keys = keys;
        S = stringLocalizer;
    }

    /// <summary>
    /// Starts a learner's (or a member of the course staff's) launch of an External tool item: checks access, works out
    /// the roles, and sends the browser to the tool's login initiation URL with a signed message hint.
    /// </summary>
    /// <param name="itemId">The content item id of the External tool item.</param>
    [HttpGet("learning/lti/launch/{itemId}", Name = "LearningLtiLaunch")]
    public async Task<IActionResult> Launch(string itemId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var item = await _contentManager.GetAsync(itemId, VersionOptions.Published);
        var part = item?.Get<ExternalToolItemPart>(nameof(ExternalToolItemPart));

        if (part is null)
        {
            return NotFound();
        }

        var tool = string.IsNullOrEmpty(part.ToolId) ? null : await _tools.FindByIdAsync(part.ToolId);

        if (tool is not { Enabled: true })
        {
            return LaunchError(S["This activity is not available right now."]);
        }

        var access = await _courseAccess.GetItemAccessAsync(User, item);
        var enrollment = access.Enrollment is { Status: EnrollmentStatus.Active } active && active.UserId == userId ? active : null;
        var isStaff = access.Reason == CourseItemAccessReason.Staff;

        if (!access.Allowed || (enrollment is null && !isStaff))
        {
            return LaunchError(access.LockedReason ?? S["You need an active enrollment in the course to open this activity."]);
        }

        var roles = await RolesAsync(access.CourseId, userId, enrollment is not null, isStaff);
        var platform = await _platform.GetAsync();
        var hint = _hints.Create(new LtiMessageHint
        {
            MessageType = LtiConstants.MessageTypes.ResourceLinkRequest,
            ToolId = tool.ItemId,
            UserId = userId,
            CourseId = access.CourseId,
            ItemId = item.ContentItemId,
            EnrollmentId = enrollment?.ItemId,
            Roles = roles.ToList(),
            ReturnUrl = Url.RouteUrl("LearningPlayer", new { courseId = access.CourseId, itemId = item.ContentItemId }, Request.Scheme),
        });

        var targetLinkUri = string.IsNullOrWhiteSpace(part.TargetLinkUri) ? tool.TargetLinkUri : part.TargetLinkUri;

        NoStore();

        return Redirect(LtiLoginInitiation.BuildUrl(tool, platform, LtiSubjects.For(tool, userId), hint, targetLinkUri));
    }

    /// <summary>
    /// The authorization endpoint of the third-party initiated login. The tool's authentication request arrives here (a
    /// redirect, so the sign-in cookie comes with it); the answer is a page that posts the id token, or the error, to the
    /// tool's redirect URI.
    /// </summary>
    [HttpGet("learning/lti/authorize", Name = "LearningLtiAuthorize")]
    [HttpPost("learning/lti/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var parameters = Request.HasFormContentType ? (IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>>)Request.Form : Request.Query;
        var values = parameters.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);

        var result = await _authorization.AuthorizeAsync(new LtiAuthorizationRequest
        {
            Scope = Get(values, "scope"),
            ResponseType = Get(values, "response_type"),
            ResponseMode = Get(values, "response_mode"),
            Prompt = Get(values, "prompt"),
            ClientId = Get(values, "client_id"),
            RedirectUri = Get(values, "redirect_uri"),
            LoginHint = Get(values, "login_hint"),
            LtiMessageHint = Get(values, "lti_message_hint"),
            State = Get(values, "state"),
            Nonce = Get(values, "nonce"),
        }, User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted);

        NoStore();

        if (string.IsNullOrEmpty(result.RedirectUri))
        {
            return LaunchError(S["The tool's login request was refused: {0}", result.ErrorDescription], StatusCodes.Status400BadRequest);
        }

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        if (result.Succeeded)
        {
            fields["id_token"] = result.IdToken;
        }
        else
        {
            fields["error"] = result.Error;
            fields["error_description"] = result.ErrorDescription;
        }

        if (!string.IsNullOrEmpty(result.State))
        {
            fields["state"] = result.State;
        }

        return FormPost(result.RedirectUri, fields, S["Opening the tool…"], S["Continue"]);
    }

    /// <summary>
    /// The platform's JSON Web Key Set: the key that signs launches now and those retired recently.
    /// </summary>
    [HttpGet("learning/lti/jwks", Name = "LearningLtiJwks")]
    public async Task<IActionResult> Jwks()
    {
        var keySet = await _keys.GetJsonWebKeySetAsync(HttpContext.RequestAborted);

        Response.Headers.CacheControl = "public, max-age=300";

        return Content(keySet.ToJsonString(), "application/json");
    }

    /// <summary>
    /// Writes a page that posts fields to an address as soon as it loads, with a button for browsers without script.
    /// </summary>
    internal ContentResult FormPost(string action, IReadOnlyDictionary<string, string> fields, string message, string button)
    {
        var html = new StringBuilder();
        var encoder = HtmlEncoder.Default;

        html.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\"><title>")
            .Append(encoder.Encode(message))
            .Append("</title></head><body><form method=\"post\" action=\"")
            .Append(encoder.Encode(action))
            .Append("\"><p>")
            .Append(encoder.Encode(message))
            .Append("</p>");

        foreach (var (name, value) in fields)
        {
            html.Append("<input type=\"hidden\" name=\"")
                .Append(encoder.Encode(name))
                .Append("\" value=\"")
                .Append(encoder.Encode(value ?? string.Empty))
                .Append("\">");
        }

        html.Append("<noscript><button type=\"submit\">")
            .Append(encoder.Encode(button))
            .Append("</button></noscript></form><script>document.forms[0].submit();</script></body></html>");

        Response.Headers["Referrer-Policy"] = "no-referrer";

        return Content(html.ToString(), "text/html; charset=utf-8");
    }

    internal IActionResult LaunchError(string message, int statusCode = StatusCodes.Status200OK)
    {
        Response.StatusCode = statusCode;

        return View("LaunchError", message);
    }

    private async Task<IReadOnlyList<string>> RolesAsync(string courseId, string userId, bool learner, bool isStaff)
    {
        var course = await _courseStructure.GetCourseAsync(courseId);
        var staffRoles = course?.Get<CoursePart>(nameof(CoursePart))?.Staff?
            .Where(member => string.Equals(member.UserId, userId, StringComparison.Ordinal))
            .Select(member => member.Role)
            .ToArray() ?? [];

        var courseManager = isStaff && staffRoles.Length == 0 && course is not null &&
            await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageOwnCourses, course);

        return LtiRoles.For(learner, staffRoles, courseManager);
    }

    private void NoStore()
        => Response.Headers.CacheControl = "no-store";

    private static string Get(Dictionary<string, string> values, string name)
        => values.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value) ? value : null;
}
