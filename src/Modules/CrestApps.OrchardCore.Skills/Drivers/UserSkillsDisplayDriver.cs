using CrestApps.OrchardCore.Skills.Core.Drivers;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Skills.Drivers;

/// <summary>
/// Adds a Skills section to the user editor, with one proficiency choice per enabled skill. Only users allowed to
/// assign skills see it, and only once the user exists.
/// </summary>
internal sealed class UserSkillsDisplayDriver : SkillSubjectSkillsDisplayDriverBase<User>
{
    public UserSkillsDisplayDriver(
        ISkillManager skillManager,
        ISkillAssignmentService assignmentService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
        : base(skillManager, assignmentService, httpContextAccessor, authorizationService)
    {
    }

    protected override string ShapeType => "UserSkills_Edit";

    protected override string Location => "Content:12";

    protected override SkillSubject GetSubject(User model, bool isNew)
        => string.IsNullOrEmpty(model.UserId) ? default : SkillSubject.User(model.UserId);
}
