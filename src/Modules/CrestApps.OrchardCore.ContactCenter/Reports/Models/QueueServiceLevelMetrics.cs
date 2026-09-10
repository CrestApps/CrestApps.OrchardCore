namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

internal sealed class QueueServiceLevelMetrics
{
    public long EligibleOffered { get; set; }

    public long Answered { get; set; }

    public long ServiceLevelEligibleOffered { get; set; }

    public long AnsweredWithinThreshold { get; set; }

    public double AnswerSpeedSeconds { get; set; }

    public bool HasServiceLevel { get; set; }

    public double ServiceLevel => HasServiceLevel ? (double)AnsweredWithinThreshold / ServiceLevelEligibleOffered : 0d;

    public double AverageSpeedOfAnswerSeconds => Answered > 0 ? AnswerSpeedSeconds / Answered : 0d;
}
