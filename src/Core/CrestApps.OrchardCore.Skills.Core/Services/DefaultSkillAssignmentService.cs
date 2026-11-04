using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// The default <see cref="ISkillAssignmentService"/>: one assignment per skill and person, checked against the
/// catalog, with every change passed to the <see cref="ISkillAssignmentHandler"/>s before the session commits.
/// </summary>
internal sealed class DefaultSkillAssignmentService : ISkillAssignmentService
{
    private readonly SkillAssignmentStore _store;
    private readonly SkillStore _skillStore;
    private readonly CertificationRecordStore _recordStore;
    private readonly IEnumerable<ISkillAssignmentHandler> _handlers;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultSkillAssignmentService(
        SkillAssignmentStore store,
        SkillStore skillStore,
        CertificationRecordStore recordStore,
        IEnumerable<ISkillAssignmentHandler> handlers,
        IClock clock,
        IStringLocalizer<DefaultSkillAssignmentService> stringLocalizer)
    {
        _store = store;
        _skillStore = skillStore;
        _recordStore = recordStore;
        _handlers = handlers;
        _clock = clock;
        S = stringLocalizer;
    }

    public Task<IReadOnlyList<SkillAssignment>> GetAssignmentsAsync(SkillSubject subject, CancellationToken cancellationToken = default)
    {
        if (subject.IsEmpty)
        {
            return Task.FromResult<IReadOnlyList<SkillAssignment>>([]);
        }

        return _store.ListForSubjectAsync(subject, cancellationToken);
    }

    public async Task<IReadOnlyList<SkillAssignment>> GetEffectiveAsync(SkillSubject subject, DateOnly date, CancellationToken cancellationToken = default)
    {
        var assignments = (await GetAssignmentsAsync(subject, cancellationToken))
            .Where(assignment => assignment.IsEffectiveOn(date))
            .ToArray();

        if (assignments.Length == 0)
        {
            return [];
        }

        var skills = (await _skillStore.GetAsync(assignments.Select(assignment => assignment.SkillId), cancellationToken))
            .Where(skill => skill.Enabled)
            .ToDictionary(skill => skill.ItemId, StringComparer.Ordinal);

        IReadOnlyList<CertificationRecord> records = null;
        var effective = new List<SkillAssignment>();

        foreach (var assignment in assignments)
        {
            if (!skills.TryGetValue(assignment.SkillId, out var skill))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(skill.RequiredCertificationId))
            {
                records ??= await _recordStore.ListForSubjectAsync(subject, cancellationToken);

                if (!records.Any(record => record.CertificationId == skill.RequiredCertificationId && record.IsValidOn(date)))
                {
                    continue;
                }
            }

            effective.Add(assignment);
        }

        return effective;
    }

    public async Task<SkillsResult> SetAsync(SkillSubject subject, IEnumerable<SkillAssignmentInput> assignments, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        var result = new SkillsResult();
        var inputs = assignments.Where(input => input is not null).ToArray();

        if (subject.IsEmpty)
        {
            result.Fail(S["Choose who holds the skills."]);

            return result;
        }

        if (inputs.GroupBy(input => input.SkillId, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            result.Fail(S["A skill can be given only once."]);

            return result;
        }

        await ValidateAsync(inputs, result, cancellationToken);

        if (!result.Succeeded)
        {
            return result;
        }

        var existing = (await _store.ListForSubjectAsync(subject, cancellationToken)).ToDictionary(assignment => assignment.SkillId, StringComparer.Ordinal);
        var changed = new List<SkillAssignment>();
        var current = new List<SkillAssignment>();

        foreach (var input in inputs)
        {
            existing.Remove(input.SkillId, out var assignment);

            if (assignment is not null && Matches(assignment, input))
            {
                current.Add(assignment);

                continue;
            }

            assignment = await SaveAsync(subject, assignment, input, actor, cancellationToken);
            changed.Add(assignment);
            current.Add(assignment);
        }

        foreach (var removed in existing.Values)
        {
            _store.Delete(removed);
        }

        if (changed.Count > 0 || existing.Count > 0)
        {
            await RaiseAsync(new SkillAssignmentsChangedContext
            {
                Subject = subject,
                Assignments = current,
                Changed = changed,
                RemovedSkillIds = existing.Keys.ToArray(),
                Actor = actor,
            }, cancellationToken);
        }

        return result;
    }

    public async Task<SkillsResult> AssignAsync(SkillSubject subject, SkillAssignmentInput assignment, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var result = new SkillsResult();

        if (subject.IsEmpty)
        {
            result.Fail(S["Choose who holds the skill."]);

            return result;
        }

        await ValidateAsync([assignment], result, cancellationToken);

        if (!result.Succeeded)
        {
            return result;
        }

        var all = (await _store.ListForSubjectAsync(subject, cancellationToken)).ToList();
        var existing = all.FirstOrDefault(item => item.SkillId == assignment.SkillId);

        if (existing is not null && Matches(existing, assignment))
        {
            return result;
        }

        var saved = await SaveAsync(subject, existing, assignment, actor, cancellationToken);

        if (existing is null)
        {
            all.Add(saved);
        }

        await RaiseAsync(new SkillAssignmentsChangedContext
        {
            Subject = subject,
            Assignments = all,
            Changed = [saved],
            Actor = actor,
        }, cancellationToken);

        return result;
    }

    public async Task<bool> RemoveAsync(SkillSubject subject, string skillId, string actor, CancellationToken cancellationToken = default)
    {
        if (subject.IsEmpty || string.IsNullOrEmpty(skillId))
        {
            return false;
        }

        var all = (await _store.ListForSubjectAsync(subject, cancellationToken)).ToList();
        var existing = all.FirstOrDefault(item => item.SkillId == skillId);

        if (existing is null)
        {
            return false;
        }

        _store.Delete(existing);
        all.Remove(existing);

        await RaiseAsync(new SkillAssignmentsChangedContext
        {
            Subject = subject,
            Assignments = all,
            RemovedSkillIds = [skillId],
            Actor = actor,
        }, cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<SkillAssignment>> FindHoldersAsync(string skillId, int minimumProficiency, DateOnly date, SkillSubjectKind kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillId);

        var skill = await _skillStore.FindByIdAsync(skillId, cancellationToken);

        if (skill is null || !skill.Enabled)
        {
            return [];
        }

        var dateKey = SkillsDates.ToKey(date);
        var holders = await _store.ListHoldersAsync(skillId, minimumProficiency, dateKey, kind, cancellationToken);

        if (string.IsNullOrEmpty(skill.RequiredCertificationId) || holders.Count == 0)
        {
            return holders;
        }

        var certified = (await _recordStore.ListValidSubjectIdsAsync(skill.RequiredCertificationId, kind, dateKey, cancellationToken)).ToHashSet(StringComparer.Ordinal);

        return holders.Where(holder => certified.Contains(holder.SubjectId)).ToArray();
    }

    public async Task<bool> HoldsAsync(SkillSubject subject, string skillId, int minimumProficiency, DateOnly date, CancellationToken cancellationToken = default)
    {
        var effective = await GetEffectiveAsync(subject, date, cancellationToken);

        return effective.Any(assignment => assignment.SkillId == skillId && assignment.Proficiency >= minimumProficiency);
    }

    private async Task ValidateAsync(IReadOnlyCollection<SkillAssignmentInput> inputs, SkillsResult result, CancellationToken cancellationToken)
    {
        if (inputs.Any(input => string.IsNullOrEmpty(input.SkillId)))
        {
            result.Fail(S["Choose a skill."]);

            return;
        }

        var skills = (await _skillStore.GetAsync(inputs.Select(input => input.SkillId).Distinct(StringComparer.Ordinal), cancellationToken))
            .ToDictionary(skill => skill.ItemId, StringComparer.Ordinal);

        foreach (var input in inputs)
        {
            if (!skills.TryGetValue(input.SkillId, out var skill))
            {
                result.Fail(S["The skill '{0}' doesn't exist.", input.SkillId]);

                continue;
            }

            if (!skill.Enabled)
            {
                result.Fail(S["The skill '{0}' is disabled.", skill.Name]);
            }

            if (!SkillProficiency.IsValid(input.Proficiency))
            {
                result.Fail(S["The proficiency of '{0}' must be from {1} to {2}.", skill.Name, SkillProficiency.Minimum, SkillProficiency.Maximum]);
            }

            if (input.EffectiveFrom is not null && input.EffectiveTo is not null && input.EffectiveTo < input.EffectiveFrom)
            {
                result.Fail(S["'{0}' ends before it starts.", skill.Name]);
            }
        }
    }

    private async Task<SkillAssignment> SaveAsync(SkillSubject subject, SkillAssignment assignment, SkillAssignmentInput input, string actor, CancellationToken cancellationToken)
    {
        var isNew = assignment is null;

        assignment ??= new SkillAssignment
        {
            SubjectKind = subject.Kind,
            SubjectId = subject.Id,
            SkillId = input.SkillId,
        };

        assignment.Proficiency = input.Proficiency;
        assignment.EffectiveFrom = input.EffectiveFrom;
        assignment.EffectiveTo = input.EffectiveTo;
        assignment.AssignedBy = actor;
        assignment.AssignedUtc = _clock.UtcNow;

        if (isNew)
        {
            await _store.CreateAsync(assignment, cancellationToken);
        }
        else
        {
            await _store.UpdateAsync(assignment, cancellationToken);
        }

        return assignment;
    }

    private async Task RaiseAsync(SkillAssignmentsChangedContext context, CancellationToken cancellationToken)
    {
        foreach (var handler in _handlers)
        {
            await handler.AssignmentsChangedAsync(context, cancellationToken);
        }
    }

    private static bool Matches(SkillAssignment assignment, SkillAssignmentInput input)
    {
        return assignment.Proficiency == input.Proficiency
            && assignment.EffectiveFrom == input.EffectiveFrom
            && assignment.EffectiveTo == input.EffectiveTo;
    }
}
