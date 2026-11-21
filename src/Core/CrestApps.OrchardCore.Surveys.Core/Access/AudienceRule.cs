using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Decides who may respond: anyone, signed-in users with the Respond to surveys permission, members of the chosen user
/// audiences, or invitation holders only.
/// </summary>
public sealed class AudienceRule : ISurveyAccessRule
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudienceRule"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, to find the audience service and the authorization service.</param>
    public AudienceRule(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public string Name => "Audience";

    /// <inheritdoc />
    public int Order => SurveyAccessRuleOrder.Audience;

    /// <inheritdoc />
    public async Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
    {
        if (context.IsTest)
        {
            return SurveyAccessDecision.Allow;
        }

        var access = context.Survey.Access ?? new SurveyAccessSettings();
        var userId = context.Identity?.UserId;

        switch (access.Audience)
        {
            case SurveyAudienceMode.SignedIn:
                if (string.IsNullOrEmpty(userId))
                {
                    return SurveyAccessDecision.Deny(SurveyErrorCode.SignInRequired);
                }

                return await CanRespondAsync(context) ? SurveyAccessDecision.Allow : SurveyAccessDecision.Deny(SurveyErrorCode.NotInAudience);

            case SurveyAudienceMode.Audiences:
                if (string.IsNullOrEmpty(userId))
                {
                    return SurveyAccessDecision.Deny(SurveyErrorCode.SignInRequired);
                }

                var audiences = _serviceProvider.GetService<IUserAudienceService>();

                if (audiences is null || access.AudienceIds is not { Count: > 0 })
                {
                    return SurveyAccessDecision.Deny(SurveyErrorCode.NotInAudience);
                }

                foreach (var audience in access.AudienceIds)
                {
                    if (await audiences.IsMemberAsync(audience, userId))
                    {
                        return SurveyAccessDecision.Allow;
                    }
                }

                return SurveyAccessDecision.Deny(SurveyErrorCode.NotInAudience);

            case SurveyAudienceMode.InvitationOnly:
                return context.Invitation is null ? SurveyAccessDecision.Deny(SurveyErrorCode.InvitationInvalid) : SurveyAccessDecision.Allow;

            default:
                return SurveyAccessDecision.Allow;
        }
    }

    private async Task<bool> CanRespondAsync(SurveyAccessContext context)
    {
        var principal = context.Identity is SurveyRespondentIdentity identity ? PrincipalOf(identity) : null;
        var authorization = _serviceProvider.GetService<IAuthorizationService>();

        if (principal is null || authorization is null)
        {
            return true;
        }

        return await authorization.AuthorizeAsync(principal, SurveysPermissions.RespondToSurveys);
    }

    private ClaimsPrincipal PrincipalOf(SurveyRespondentIdentity identity)
        => _serviceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext?.User is { Identity.IsAuthenticated: true } user
            && string.Equals(user.FindFirstValue(ClaimTypes.NameIdentifier), identity.UserId, StringComparison.Ordinal)
                ? user
                : null;
}
