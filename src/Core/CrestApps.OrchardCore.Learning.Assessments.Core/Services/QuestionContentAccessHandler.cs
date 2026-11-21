using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Keeps questions and banks private. Orchard Core only grants View content, so a role holding it could read any
/// question and its key by the display route, the content API, GraphQL or a data source; this handler fails the view,
/// preview and own-view permissions on a question or bank for everyone but people who author questions, see answer keys
/// or grade.
/// </summary>
public sealed class QuestionContentAccessHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly Lazy<IAuthorizationService> _authorizationService;
    private readonly QuestionTypeOptions _questionTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionContentAccessHandler"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service, resolved late to avoid a cycle.</param>
    /// <param name="questionTypes">The question types.</param>
    public QuestionContentAccessHandler(Lazy<IAuthorizationService> authorizationService, IOptions<QuestionTypeOptions> questionTypes)
    {
        _authorizationService = authorizationService;
        _questionTypes = questionTypes.Value;
    }

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.Resource is not ContentItem item || !IsViewPermission(requirement) || !IsPrivate(item.ContentType))
        {
            return;
        }

        var user = context.User;

        if (user?.Identity?.IsAuthenticated == true
            && (await _authorizationService.Value.AuthorizeAsync(user, AssessmentPermissions.ViewQuestionBanks)
                || await _authorizationService.Value.AuthorizeAsync(user, AssessmentPermissions.GradeAssessments)))
        {
            context.Succeed(requirement);

            return;
        }

        context.Fail(new AuthorizationFailureReason(this, "Questions are visible only to people who author or grade them."));
    }

    private bool IsPrivate(string contentType)
        => string.Equals(contentType, AssessmentsConstants.ContentTypes.QuestionBank, StringComparison.Ordinal) || _questionTypes.IsQuestion(contentType);

    private static bool IsViewPermission(PermissionRequirement requirement)
        => requirement.Permission.Name == CommonPermissions.ViewContent.Name
            || requirement.Permission.Name == CommonPermissions.ViewOwnContent.Name
            || requirement.Permission.Name == CommonPermissions.PreviewContent.Name;
}
