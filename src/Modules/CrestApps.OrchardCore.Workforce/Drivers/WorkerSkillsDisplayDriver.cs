using CrestApps.OrchardCore.Skills.Core.Drivers;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Adds the Skills card to the worker editor, through the Skills building block's section driver. A new worker gets
/// its skills in the same save, because its id exists before it is stored; the editor cancels the session when the
/// worker is not saved.
/// </summary>
internal sealed class WorkerSkillsDisplayDriver : SkillSubjectSkillsDisplayDriverBase<WorkerProfile>
{
    public WorkerSkillsDisplayDriver(
        ISkillManager skillManager,
        ISkillAssignmentService assignmentService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
        : base(skillManager, assignmentService, httpContextAccessor, authorizationService)
    {
    }

    protected override string ShapeType => "WorkerSkills_Edit";

    protected override SkillSubject GetSubject(WorkerProfile model, bool isNew)
        => SkillSubject.Worker(model.ItemId);
}
