using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Core.Services.Ags;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.Learning.Lti.Controllers;

/// <summary>
/// The LTI Advantage services of a course, for registered tools with an access token from the OpenID server's token
/// endpoint (client credentials with a <c>private_key_jwt</c> client assertion): Names and Role Provisioning Services 2.0
/// (the context memberships) and Assignment and Grade Services 2.0 (line items, scores and results). Each request checks
/// the token's scope, that the tool may use the service, and that the tool is used in the course.
/// </summary>
[IgnoreAntiforgeryToken]
public sealed class LtiServicesController : Controller
{
    private const long MaxBodyBytes = 64 * 1024;

    private readonly LtiServiceAccess _access;
    private readonly LtiAgsService _ags;
    private readonly LtiCourseMembers _members;
    private readonly ICourseStructureService _courseStructure;
    private readonly ILtiPlatform _platform;

    public LtiServicesController(
        LtiServiceAccess access,
        LtiAgsService ags,
        LtiCourseMembers members,
        ICourseStructureService courseStructure,
        ILtiPlatform platform)
    {
        _access = access;
        _ags = ags;
        _members = members;
        _courseStructure = courseStructure;
        _platform = platform;
    }

    /// <summary>
    /// The context memberships service: the course's staff and learners, paged, filtered by role, within the tool's
    /// privacy level.
    /// </summary>
    [HttpGet("learning/lti/nrps/{courseId}/memberships", Name = "LearningLtiMemberships")]
    public async Task<IActionResult> Memberships(string courseId, string role, string rlid, string limit, string page)
    {
        var (caller, refusal) = await AuthorizeAsync(courseId, [LtiConstants.Scopes.ContextMembershipReadOnly], tool => tool.AllowNamesAndRoles);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!string.IsNullOrEmpty(rlid) && await _access.FindResourceLinkAsync(caller.Tool, courseId, rlid) is null)
        {
            return Error(StatusCodes.Status400BadRequest, "rlid is not a resource link of this tool in this course.");
        }

        var (size, number) = LtiPaging.Read(limit, page);
        var members = (await _members.ListAsync(courseId, HttpContext.RequestAborted))
            .Where(member => string.IsNullOrEmpty(role) || member.Roles.Any(candidate => candidate == role || candidate.EndsWith("#" + role, StringComparison.Ordinal)))
            .ToList();
        var pageMembers = members.Skip((number - 1) * size).Take(size).ToList();

        if (caller.Tool.PrivacyLevel > Models.LtiPrivacyLevel.Anonymous)
        {
            await _members.FillNamesAsync(pageMembers, HttpContext.RequestAborted);
        }

        var course = await _courseStructure.GetCourseAsync(courseId);
        var url = Request.GetEncodedUrl();
        var body = new JsonObject
        {
            ["id"] = url,
            ["context"] = new JsonObject
            {
                ["id"] = courseId,
                ["title"] = course?.DisplayText,
            },
            ["members"] = new JsonArray(pageMembers.Select(member => (JsonNode)LtiServiceJson.Member(member, caller.Tool)).ToArray()),
        };

        return Json(body, LtiConstants.MediaTypes.MembershipContainer, LtiPaging.Link(url, size, number, members.Count));
    }

    /// <summary>
    /// Lists the tool's line items in the course, filtered by resource link, resource id and tag, paged.
    /// </summary>
    [HttpGet("learning/lti/ags/{courseId}/lineitems", Name = "LearningLtiLineItems")]
    public async Task<IActionResult> LineItems(string courseId, [FromQuery(Name = "resource_link_id")] string resourceLinkId, [FromQuery(Name = "resource_id")] string resourceId, string tag, string limit, string page)
    {
        var (caller, refusal) = await AuthorizeAsync(courseId, [LtiConstants.Scopes.LineItem, LtiConstants.Scopes.LineItemReadOnly], tool => tool.AllowGrades);

        if (refusal is not null)
        {
            return refusal;
        }

        var (size, number) = LtiPaging.Read(limit, page);
        var (count, items) = await _ags.ListAsync(caller.Tool, courseId, resourceLinkId, resourceId, tag, number, size, HttpContext.RequestAborted);
        var platform = await _platform.GetAsync();

        return Json(new JsonArray(items.Select(item => (JsonNode)LtiServiceJson.LineItem(platform, item)).ToArray()), LtiConstants.MediaTypes.LineItemContainer, LtiPaging.Link(Request.GetEncodedUrl(), size, number, count));
    }

    /// <summary>
    /// Makes a line item in the course.
    /// </summary>
    [HttpPost("learning/lti/ags/{courseId}/lineitems")]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> CreateLineItem(string courseId)
    {
        var (caller, refusal) = await AuthorizeAsync(courseId, [LtiConstants.Scopes.LineItem], tool => tool.AllowGrades);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!LtiAgsRules.TryReadLineItem(await ReadBodyAsync(), out var input, out var error))
        {
            return Error(StatusCodes.Status400BadRequest, error);
        }

        var (lineItem, refused) = await _ags.CreateAsync(caller.Tool, courseId, input, HttpContext.RequestAborted);

        if (refused is not null)
        {
            return Error(StatusCodes.Status400BadRequest, refused);
        }

        var json = LtiServiceJson.LineItem(await _platform.GetAsync(), lineItem);

        Response.Headers.Location = json["id"]?.GetValue<string>();

        return Json(json, LtiConstants.MediaTypes.LineItem, null, StatusCodes.Status201Created);
    }

    /// <summary>
    /// Reads a line item.
    /// </summary>
    [HttpGet("learning/lti/ags/{courseId}/lineitems/{lineItemId}", Name = "LearningLtiLineItem")]
    public async Task<IActionResult> LineItem(string courseId, string lineItemId)
    {
        var (caller, refusal) = await AuthorizeAsync(courseId, [LtiConstants.Scopes.LineItem, LtiConstants.Scopes.LineItemReadOnly], tool => tool.AllowGrades);

        if (refusal is not null)
        {
            return refusal;
        }

        var lineItem = await _ags.GetAsync(caller.Tool, courseId, lineItemId, HttpContext.RequestAborted);

        return lineItem is null
            ? Error(StatusCodes.Status404NotFound, "No such line item.")
            : Json(LtiServiceJson.LineItem(await _platform.GetAsync(), lineItem), LtiConstants.MediaTypes.LineItem, null);
    }

    /// <summary>
    /// Changes a line item.
    /// </summary>
    [HttpPut("learning/lti/ags/{courseId}/lineitems/{lineItemId}")]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> UpdateLineItem(string courseId, string lineItemId)
    {
        var (caller, refusal) = await AuthorizeAsync(courseId, [LtiConstants.Scopes.LineItem], tool => tool.AllowGrades);

        if (refusal is not null)
        {
            return refusal;
        }

        var lineItem = await _ags.GetAsync(caller.Tool, courseId, lineItemId, HttpContext.RequestAborted);

        if (lineItem is null)
        {
            return Error(StatusCodes.Status404NotFound, "No such line item.");
        }

        if (!LtiAgsRules.TryReadLineItem(await ReadBodyAsync(), out var input, out var error))
        {
            return Error(StatusCodes.Status400BadRequest, error);
        }

        var (updated, refused) = await _ags.UpdateAsync(caller.Tool, lineItem, input, HttpContext.RequestAborted);

        return refused is not null
            ? Error(StatusCodes.Status400BadRequest, refused)
            : Json(LtiServiceJson.LineItem(await _platform.GetAsync(), updated), LtiConstants.MediaTypes.LineItem, null);
    }

    /// <summary>
    /// Removes a line item and its scores.
    /// </summary>
    [HttpDelete("learning/lti/ags/{courseId}/lineitems/{lineItemId}")]
    public async Task<IActionResult> DeleteLineItem(string courseId, string lineItemId)
    {
        var (caller, refusal) = await AuthorizeAsync(courseId, [LtiConstants.Scopes.LineItem], tool => tool.AllowGrades);

        if (refusal is not null)
        {
            return refusal;
        }

        var lineItem = await _ags.GetAsync(caller.Tool, courseId, lineItemId, HttpContext.RequestAborted);

        if (lineItem is null)
        {
            return Error(StatusCodes.Status404NotFound, "No such line item.");
        }

        await _ags.DeleteAsync(lineItem, HttpContext.RequestAborted);

        return NoContent();
    }

    /// <summary>
    /// Posts a learner's score on a line item.
    /// </summary>
    [HttpPost("learning/lti/ags/{courseId}/lineitems/{lineItemId}/scores", Name = "LearningLtiScores")]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Scores(string courseId, string lineItemId)
    {
        var (caller, refusal) = await AuthorizeAsync(courseId, [LtiConstants.Scopes.Score], tool => tool.AllowGrades);

        if (refusal is not null)
        {
            return refusal;
        }

        var lineItem = await _ags.GetAsync(caller.Tool, courseId, lineItemId, HttpContext.RequestAborted);

        if (lineItem is null)
        {
            return Error(StatusCodes.Status404NotFound, "No such line item.");
        }

        if (!LtiAgsRules.TryReadScore(await ReadBodyAsync(), out var score, out var error))
        {
            return Error(StatusCodes.Status400BadRequest, error);
        }

        return await _ags.PostScoreAsync(caller.Tool, lineItem, score, HttpContext.RequestAborted) switch
        {
            LtiScoreOutcome.UnknownUser => Error(StatusCodes.Status400BadRequest, "userId is not a member of this course for this tool."),
            LtiScoreOutcome.NotLater => Error(StatusCodes.Status409Conflict, "The timestamp is not later than the score already stored for this user."),
            _ => NoContent(),
        };
    }

    /// <summary>
    /// Reads the results of a line item, optionally of one learner, paged.
    /// </summary>
    [HttpGet("learning/lti/ags/{courseId}/lineitems/{lineItemId}/results", Name = "LearningLtiResults")]
    public async Task<IActionResult> Results(string courseId, string lineItemId, [FromQuery(Name = "user_id")] string userId, string limit, string page)
    {
        var (caller, refusal) = await AuthorizeAsync(courseId, [LtiConstants.Scopes.ResultReadOnly], tool => tool.AllowGrades);

        if (refusal is not null)
        {
            return refusal;
        }

        var lineItem = await _ags.GetAsync(caller.Tool, courseId, lineItemId, HttpContext.RequestAborted);

        if (lineItem is null)
        {
            return Error(StatusCodes.Status404NotFound, "No such line item.");
        }

        var (size, number) = LtiPaging.Read(limit, page);
        var (count, scores) = await _ags.ResultsAsync(caller.Tool, lineItem, userId, number, size, HttpContext.RequestAborted);
        var platform = await _platform.GetAsync();

        return Json(new JsonArray(scores.Select(score => (JsonNode)LtiServiceJson.Result(platform, lineItem, score)).ToArray()), LtiConstants.MediaTypes.ResultContainer, LtiPaging.Link(Request.GetEncodedUrl(), size, number, count));
    }

    private async Task<(LtiServiceCaller Caller, IActionResult Refusal)> AuthorizeAsync(string courseId, string[] anyOfScopes, Func<Models.ExternalTool, bool> allowed)
    {
        Response.Headers.CacheControl = "no-store";

        var caller = await _access.AuthenticateAsync(HttpContext);

        if (caller is null)
        {
            Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";

            return (null, Error(StatusCodes.Status401Unauthorized, "A valid access token of a registered external tool is required."));
        }

        if (!anyOfScopes.Any(caller.HasScope))
        {
            Response.Headers.WWWAuthenticate = $"Bearer error=\"insufficient_scope\", scope=\"{string.Join(' ', anyOfScopes)}\"";

            return (null, Error(StatusCodes.Status403Forbidden, "The access token does not have the scope this service needs."));
        }

        if (!allowed(caller.Tool))
        {
            return (null, Error(StatusCodes.Status403Forbidden, "The tool may not use this service."));
        }

        if (!await _access.IsToolInCourseAsync(caller.Tool, courseId))
        {
            return (null, Error(StatusCodes.Status404NotFound, "The tool is not used in this course."));
        }

        return (caller, null);
    }

    private async Task<JsonNode> ReadBodyAsync()
    {
        try
        {
            return await JsonNode.ParseAsync(Request.Body, cancellationToken: HttpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private ContentResult Json(JsonNode body, string mediaType, string link, int statusCode = StatusCodes.Status200OK)
    {
        if (!string.IsNullOrEmpty(link))
        {
            Response.Headers.Link = link;
        }

        return new ContentResult
        {
            Content = body.ToJsonString(),
            ContentType = mediaType,
            StatusCode = statusCode,
        };
    }

    private static ContentResult Error(int statusCode, string detail)
        => new()
        {
            Content = new JsonObject { ["error"] = statusCode switch { 401 => "invalid_token", 403 => "forbidden", 404 => "not_found", 409 => "conflict", _ => "invalid_request" }, ["error_description"] = detail }.ToJsonString(),
            ContentType = "application/json",
            StatusCode = statusCode,
        };
}
