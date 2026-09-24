using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Totals an agent's state intervals by state, and the groupings the workforce and payroll reports show.
/// </summary>
internal sealed class AgentTimeSummary
{
    public double SignedInSeconds { get; private set; }

    public double AvailableSeconds { get; private set; }

    public double ReservedSeconds { get; private set; }

    public double BusySeconds { get; private set; }

    public double WrapUpSeconds { get; private set; }

    public double BreakSeconds { get; private set; }

    public double AwaySeconds { get; private set; }

    public double MeetingSeconds { get; private set; }

    public double TrainingSeconds { get; private set; }

    public double OtherNotReadySeconds { get; private set; }

    public double WorkSeconds => BusySeconds + WrapUpSeconds;

    public double ProductivePresenceSeconds => AvailableSeconds + ReservedSeconds + WorkSeconds;

    public double BreakAndAwaySeconds => BreakSeconds + AwaySeconds;

    public double MeetingAndTrainingSeconds => MeetingSeconds + TrainingSeconds;

    public double Utilization => SignedInSeconds > 0d ? WorkSeconds / SignedInSeconds : 0d;

    public static AgentTimeSummary Create(IEnumerable<AgentPresenceInterval> intervals)
    {
        var result = new AgentTimeSummary();

        foreach (var interval in intervals)
        {
            var duration = interval.DurationSeconds;

            if (interval.Status != AgentPresenceStatus.Offline)
            {
                result.SignedInSeconds += duration;
            }

            switch (interval.Status)
            {
                case AgentPresenceStatus.Available:
                    result.AvailableSeconds += duration;
                    break;
                case AgentPresenceStatus.Reserved:
                    result.ReservedSeconds += duration;
                    break;
                case AgentPresenceStatus.Busy:
                    result.BusySeconds += duration;
                    break;
                case AgentPresenceStatus.WrapUp:
                    result.WrapUpSeconds += duration;
                    break;
                case AgentPresenceStatus.Break:
                    result.BreakSeconds += duration;
                    break;
                case AgentPresenceStatus.Away:
                    result.AwaySeconds += duration;
                    break;
                case AgentPresenceStatus.Meeting:
                    result.MeetingSeconds += duration;
                    break;
                case AgentPresenceStatus.Training:
                    result.TrainingSeconds += duration;
                    break;
                case AgentPresenceStatus.Offline:
                    break;
                default:
                    result.OtherNotReadySeconds += duration;
                    break;
            }
        }

        return result;
    }
}
