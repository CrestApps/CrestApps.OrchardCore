using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Tells whether the person running an export may export responses and secrets.
/// </summary>
internal sealed class SurveysExportAuthorizer
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysExportAuthorizer"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public SurveysExportAuthorizer(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Gets whether the current user holds the Export survey responses permission. An export without a signed-in user
    /// may not.
    /// </summary>
    /// <returns><see langword="true"/> when allowed.</returns>
    public async Task<bool> CanExportResponsesAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            && await _authorizationService.AuthorizeAsync(user, SurveysPermissions.ExportSurveyResponses);
    }

    /// <summary>
    /// Gets the id of the current user, or <see langword="null"/>.
    /// </summary>
    /// <returns>The user id.</returns>
    public string CurrentUserId()
        => _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
}
