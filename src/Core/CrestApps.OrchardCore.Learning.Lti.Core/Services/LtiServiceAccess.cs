using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OrchardCore;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// Authenticates the services' requests: a bearer access token of Orchard Core's <c>Api</c> scheme (OpenID token
/// validation of the token endpoint's tokens), never the sign-in cookie, whose client is a registered, enabled external
/// tool; and checks that the tool is used in the course the request names.
/// </summary>
public sealed class LtiServiceAccess
{
    private readonly ILtiToolDirectory _tools;
    private readonly ICourseStructureService _courseStructure;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiServiceAccess"/> class.
    /// </summary>
    /// <param name="tools">The tool directory.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="logger">The logger.</param>
    public LtiServiceAccess(ILtiToolDirectory tools, ICourseStructureService courseStructure, ILogger<LtiServiceAccess> logger)
    {
        _tools = tools;
        _courseStructure = courseStructure;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a request.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <returns>The caller, or <see langword="null"/> when the request has no valid access token of a tool.</returns>
    public async Task<LtiServiceCaller> AuthenticateAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!httpContext.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        AuthenticateResult result;

        try
        {
            result = await httpContext.AuthenticateAsync(OrchardCoreConstants.AuthenticationSchemes.Api);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "The Api authentication scheme could not authenticate an LTI service request. Is OpenID token validation set up for this tenant?");

            return null;
        }

        var principal = result?.Succeeded == true ? result.Principal : null;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var clientId = principal.FindFirstValue(OpenIddictConstants.Claims.ClientId)
            ?? principal.FindFirstValue(OpenIddictConstants.Claims.AuthorizedParty)
            ?? (principal.FindFirstValue("oc:entyp") == "application" ? principal.FindFirstValue(OpenIddictConstants.Claims.Subject) : null);

        var tool = await _tools.FindByClientIdAsync(clientId, httpContext.RequestAborted);

        return tool is { Enabled: true } ? new LtiServiceCaller(tool, principal) : null;
    }

    /// <summary>
    /// Gets whether the published outline of a course has an External tool item of the tool.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="courseId">The course.</param>
    /// <returns><see langword="true"/> when the tool is used in the course.</returns>
    public async Task<bool> IsToolInCourseAsync(ExternalTool tool, string courseId)
        => await FindResourceLinkAsync(tool, courseId, resourceLinkId: null) is not null;

    /// <summary>
    /// Finds an External tool item of the tool in the published outline of a course.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="courseId">The course.</param>
    /// <param name="resourceLinkId">The item, or <see langword="null"/> for any item of the tool.</param>
    /// <returns>The item's content item id, or <see langword="null"/>.</returns>
    public async Task<string> FindResourceLinkAsync(ExternalTool tool, string courseId, string resourceLinkId)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var course = string.IsNullOrEmpty(courseId) ? null : await _courseStructure.GetCourseAsync(courseId);

        if (course is null)
        {
            return null;
        }

        var outline = await _courseStructure.GetOutlineAsync(course);

        return outline.Entries
            .Where(entry => resourceLinkId is null || entry.Item.ContentItemId == resourceLinkId)
            .FirstOrDefault(entry => entry.Item.Get<ExternalToolItemPart>(nameof(ExternalToolItemPart))?.ToolId == tool.ItemId)?
            .Item.ContentItemId;
    }
}
