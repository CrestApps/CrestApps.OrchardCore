using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Holds a publish to the rules a change of one published shift is held to: a manager never changes their own published
/// shift, nor gives one to themselves, on the strength of managing their own team, and a published shift that started
/// or ended is changed only with <see cref="WorkforcePermissions.ChangeStartedShifts"/>. Without it, editing the draft
/// and publishing changed what the published shift's own editor refuses. A publish with no signed-in user (a background
/// task, a recipe) isn't checked.
/// </summary>
internal sealed class PublishedShiftChangesPublishValidator : ISchedulePublishValidator
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWorkforceScopeService _scopeService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public PublishedShiftChangesPublishValidator(
        IHttpContextAccessor httpContextAccessor,
        IWorkforceScopeService scopeService,
        IAuthorizationService authorizationService,
        IClock clock,
        IStringLocalizer<PublishedShiftChangesPublishValidator> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _scopeService = scopeService;
        _authorizationService = authorizationService;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task ValidateAsync(SchedulePublishValidationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true || context.Changes.Count == 0)
        {
            return;
        }

        var decides = new Dictionary<string, bool>(StringComparer.Ordinal);
        var now = _clock.UtcNow;
        bool? changesStarted = null;

        foreach (var change in context.Changes)
        {
            foreach (var workerId in new[] { change.PreviousWorkerId, change.WorkerId }.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
            {
                if (!decides.TryGetValue(workerId, out var allowed))
                {
                    allowed = await _scopeService.CanDecideForWorkerAsync(user, workerId, cancellationToken);
                    decides[workerId] = allowed;
                }

                if (!allowed)
                {
                    Block(context, change, workerId, S["You can't change your own shift on the published schedule, nor give yourself one."]);
                }
            }

            if (change.Kind != ScheduleChangeKind.Added && change.BeforeStartUtc <= now)
            {
                changesStarted ??= await _authorizationService.AuthorizeAsync(user, WorkforcePermissions.ChangeStartedShifts);

                if (changesStarted == false)
                {
                    Block(context, change, change.WorkerId, S["A published shift that has started or ended changes. Changing it needs the permission to change shifts that have started or ended."]);
                }
            }
        }
    }

    private static void Block(SchedulePublishValidationContext context, SchedulePublishChange change, string workerId, string message)
    {
        context.Violations.Add(new LaborRuleViolation
        {
            RuleKey = "PublishedShiftChange",
            RuleName = message,
            Enforcement = LaborRuleEnforcement.Block,
            WorkerId = workerId,
            WorkerName = workerId is not null && context.Workers.TryGetValue(workerId, out var worker) ? worker.DisplayName : null,
            ShiftKeys = [change.ShiftKey],
            Message = message,
        });
    }
}
