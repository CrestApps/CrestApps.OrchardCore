using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Lti.Core.DeepLinking;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Lti.Controllers;

/// <summary>
/// Deep Linking 2.0 with the site as the platform: a member of the course staff starts an <c>LtiDeepLinkingRequest</c>
/// from the course editor, picks content in the tool, and the tool posts its response back here.
/// </summary>
public sealed class LtiDeepLinkingController : Controller
{
    private readonly IContentManager _contentManager;
    private readonly IExternalToolStore _tools;
    private readonly IAuthorizationService _authorizationService;
    private readonly LtiMessageHints _hints;
    private readonly ILtiPlatform _platform;
    private readonly LtiDeepLinkingService _deepLinking;
    private readonly LtiDeepLinkItemWriter _writer;
    private readonly UserManager<IUser> _userManager;
    private readonly IUserClaimsPrincipalFactory<IUser> _principalFactory;

    internal readonly IStringLocalizer S;

    public LtiDeepLinkingController(
        IContentManager contentManager,
        IExternalToolStore tools,
        IAuthorizationService authorizationService,
        LtiMessageHints hints,
        ILtiPlatform platform,
        LtiDeepLinkingService deepLinking,
        LtiDeepLinkItemWriter writer,
        UserManager<IUser> userManager,
        IUserClaimsPrincipalFactory<IUser> principalFactory,
        IStringLocalizer<LtiDeepLinkingController> stringLocalizer)
    {
        _contentManager = contentManager;
        _tools = tools;
        _authorizationService = authorizationService;
        _hints = hints;
        _platform = platform;
        _deepLinking = deepLinking;
        _writer = writer;
        _userManager = userManager;
        _principalFactory = principalFactory;
        S = stringLocalizer;
    }

    /// <summary>
    /// Starts a Deep Linking request: checks that the person may edit the course and sends the browser to the tool's
    /// login initiation URL, with the tool's Deep Linking URL as the target.
    /// </summary>
    /// <param name="courseId">The content item id of the course.</param>
    /// <param name="toolId">The id of the external tool.</param>
    [HttpGet("learning/lti/deep-linking/{courseId}/{toolId}", Name = "LearningLtiDeepLinkingStart")]
    public async Task<IActionResult> Start(string courseId, string toolId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var course = await _contentManager.GetAsync(courseId, VersionOptions.Latest);

        if (course?.Get<CoursePart>(nameof(CoursePart)) is not { } coursePart)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageOwnCourses, course))
        {
            return Forbid();
        }

        var tool = await _tools.FindByIdAsync(toolId);

        if (tool is not { Enabled: true } || string.IsNullOrWhiteSpace(tool.DeepLinkingUrl))
        {
            return NotFound();
        }

        var staffRoles = coursePart.Staff?
            .Where(member => string.Equals(member.UserId, userId, StringComparison.Ordinal))
            .Select(member => member.Role)
            .ToArray() ?? [];

        var platform = await _platform.GetAsync();
        var hint = _hints.Create(new LtiMessageHint
        {
            MessageType = LtiConstants.MessageTypes.DeepLinkingRequest,
            ToolId = tool.ItemId,
            UserId = userId,
            CourseId = course.ContentItemId,
            Roles = LtiRoles.For(learner: false, staffRoles, courseManager: staffRoles.Length == 0).ToList(),
            ReturnUrl = Url.Action("Edit", "Admin", new { area = "OrchardCore.Contents", contentItemId = course.ContentItemId }, Request.Scheme),
        });

        Response.Headers.CacheControl = "no-store";

        return Redirect(LtiLoginInitiation.BuildUrl(tool, platform, LtiSubjects.For(tool, userId), hint, tool.DeepLinkingUrl));
    }

    /// <summary>
    /// Receives the tool's Deep Linking response. The tool's page posts it from its own site, so the sign-in cookie may
    /// not come with it: the person and the course come from the signed <c>data</c> the platform put in the request, and
    /// the person's permission on the course is checked again before anything is added.
    /// </summary>
    [HttpPost("learning/lti/deep-linking/return", Name = "LearningLtiDeepLinkingReturn")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Return()
    {
        var jwt = Request.HasFormContentType ? Request.Form["JWT"].ToString() : null;
        var response = await _deepLinking.ReadResponseAsync(jwt, HttpContext.RequestAborted);

        if (!response.Succeeded)
        {
            return LtiPages.Message(this, StatusCodes.Status400BadRequest, S["The tool's content could not be added"], [S["The response was refused: {0}", response.Error]], null, null);
        }

        var user = await _userManager.FindByIdAsync(response.Data.UserId);
        var course = await _contentManager.GetAsync(response.Data.CourseId, VersionOptions.Latest);

        if (user is null || course is null ||
            !await _authorizationService.AuthorizeAsync(await _principalFactory.CreateAsync(user), LearningPermissions.ManageOwnCourses, course))
        {
            return LtiPages.Message(this, StatusCodes.Status403Forbidden, S["The tool's content could not be added"], [S["You may no longer edit this course."]], null, null);
        }

        var created = await _writer.WriteAsync(response.Tool, course.ContentItemId, response.Items, S["External tools"]);
        var lines = new List<string>
        {
            created.Count == 0
                ? S["Nothing was added to {0}.", course.DisplayText]
                : S["{0} items were added to the end of the last section of {1}: {2}. Publish the course to show them to learners.", created.Count, course.DisplayText, string.Join(", ", created.Select(item => item.DisplayText))],
        };

        if (response.RefusedTypes.Count > 0)
        {
            lines.Add(S["Not added, because only LTI resource links can be course items: {0}.", string.Join(", ", response.RefusedTypes)]);
        }

        if (!string.IsNullOrWhiteSpace(response.Message))
        {
            lines.Add(S["The tool says: {0}", response.Message]);
        }

        if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
        {
            lines.Add(S["The tool reports an error: {0}", response.ErrorMessage]);
        }

        return LtiPages.Message(this, StatusCodes.Status200OK, S["Content from {0}", response.Tool.Name], lines, response.Data.ReturnUrl, S["Back to the course"]);
    }
}
