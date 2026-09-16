namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

internal sealed class InteractionMetrics
{
    public long Total { get; set; }

    public long InboundOffered { get; set; }

    public long Answered { get; set; }

    public long InboundAnswered { get; set; }

    public long Abandoned { get; set; }

    public long Failed { get; set; }

    public long Handled { get; set; }

    public long Transferred { get; set; }

    public long AnsweredVoice { get; set; }

    public long RecordedVoice { get; set; }

    public double TalkSeconds { get; set; }

    public double WrapUpSeconds { get; set; }

    public double AnswerSpeedSeconds { get; set; }

    public double AnswerRate => Total > 0 ? (double)Answered / Total : 0d;

    public double InboundAnswerRate => InboundOffered > 0 ? (double)InboundAnswered / InboundOffered : 0d;

    public double AbandonmentRate => InboundOffered > 0 ? (double)Abandoned / InboundOffered : 0d;

    public double AverageSpeedOfAnswerSeconds => InboundAnswered > 0 ? AnswerSpeedSeconds / InboundAnswered : 0d;

    public double AverageHandleTimeSeconds => Handled > 0 ? (TalkSeconds + WrapUpSeconds) / Handled : 0d;

    public double TransferRate => Answered > 0 ? (double)Transferred / Answered : 0d;

    public double RecordingCoverage => AnsweredVoice > 0 ? (double)RecordedVoice / AnsweredVoice : 0d;
}
