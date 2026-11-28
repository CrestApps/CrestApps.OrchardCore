using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The rules the queue editor applies to what it is given before the queue is validated: skill tags are normalized and
/// deduplicated, a queue never overflows to itself, overflow targets are deduplicated and ordered by delay, counts and
/// delays are never negative and the in-queue treatment is tidied. The queue editor and the AI queue tool both apply
/// them, so a queue saved either way is stored the same way.
/// </summary>
internal static class ActivityQueueEditorRules
{
    /// <summary>
    /// Applies the editor's rules to a queue.
    /// </summary>
    /// <param name="queue">The queue, with the values that were entered.</param>
    public static void Normalize(ActivityQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        queue.RequiredSkills = SkillTag.NormalizeAll(queue.RequiredSkills);
        queue.SkillRequirements = NormalizeSkillRequirements(queue.SkillRequirements);
        queue.OverflowQueueId = string.IsNullOrWhiteSpace(queue.OverflowQueueId) || string.Equals(queue.OverflowQueueId, queue.ItemId, StringComparison.Ordinal)
            ? null
            : queue.OverflowQueueId.Trim();
        queue.OverflowAfterSeconds = Math.Max(0, queue.OverflowAfterSeconds);
        queue.OverflowTargets = NormalizeOverflowTargets(queue.OverflowTargets, queue.ItemId);
        queue.MaxQueueSize = Math.Max(0, queue.MaxQueueSize);
        queue.MaxWaitSeconds = Math.Max(0, queue.MaxWaitSeconds);
        queue.FirstResponseTargetSeconds = Math.Max(0, queue.FirstResponseTargetSeconds);
        queue.Treatment = NormalizeTreatment(queue.Treatment ?? new QueueTreatmentSettings());
    }

    private static List<QueueSkillRequirement> NormalizeSkillRequirements(IEnumerable<QueueSkillRequirement> rows)
    {
        var requirements = new List<QueueSkillRequirement>();
        var seen = new HashSet<SkillTag>();

        foreach (var row in rows ?? [])
        {
            if (row is null || !SkillTag.TryCreate(row.SkillId, out var skill) || !seen.Add(skill))
            {
                continue;
            }

            requirements.Add(new QueueSkillRequirement
            {
                SkillId = skill.Value,
                MinimumProficiency = Math.Clamp(row.MinimumProficiency, AgentSkill.MinimumProficiency, AgentSkill.MaximumProficiency),
                Required = row.Required,
                RelaxAfterSeconds = row.RelaxAfterSeconds > 0 ? row.RelaxAfterSeconds : null,
            });
        }

        return requirements;
    }

    private static List<QueueOverflowTarget> NormalizeOverflowTargets(IEnumerable<QueueOverflowTarget> rows, string queueId)
    {
        var targets = new List<QueueOverflowTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows ?? [])
        {
            var targetQueueId = row?.QueueId?.Trim();

            if (string.IsNullOrEmpty(targetQueueId) ||
                string.Equals(targetQueueId, queueId, StringComparison.OrdinalIgnoreCase) ||
                !seen.Add(targetQueueId))
            {
                continue;
            }

            targets.Add(new QueueOverflowTarget
            {
                QueueId = targetQueueId,
                AfterSeconds = Math.Max(0, row.AfterSeconds),
            });
        }

        return targets
            .OrderBy(target => target.AfterSeconds)
            .ToList();
    }

    private static QueueTreatmentSettings NormalizeTreatment(QueueTreatmentSettings treatment)
    {
        var callbackKey = treatment.CallbackDtmfKey?.Trim();
        var minimumEstimate = Math.Max(0, treatment.MinimumEstimateSeconds);

        return new QueueTreatmentSettings
        {
            WelcomeMessage = string.IsNullOrWhiteSpace(treatment.WelcomeMessage) ? null : treatment.WelcomeMessage.Trim(),
            AnnouncementIntervalSeconds = Math.Max(0, treatment.AnnouncementIntervalSeconds),
            AnnouncePosition = treatment.AnnouncePosition,
            AnnounceEstimatedWait = treatment.AnnounceEstimatedWait,
            HoldMusicMediaId = string.IsNullOrWhiteSpace(treatment.HoldMusicMediaId) ? null : treatment.HoldMusicMediaId.Trim(),
            CallbackDtmfKey = string.IsNullOrEmpty(callbackKey) ? null : callbackKey,
            CallbackOfferAfterSeconds = Math.Max(0, treatment.CallbackOfferAfterSeconds),
            MinimumEstimateSeconds = minimumEstimate,
            MaximumEstimateSeconds = Math.Max(minimumEstimate, treatment.MaximumEstimateSeconds),
            AverageHandleTimeSeconds = Math.Max(0, treatment.AverageHandleTimeSeconds),
        };
    }
}
