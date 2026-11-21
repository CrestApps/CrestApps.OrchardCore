namespace CrestApps.OrchardCore.Learning.Assessments.AuditTrail;

/// <summary>
/// What an assessments audit event records.
/// </summary>
public sealed class AssessmentsAuditData
{
    public string AttemptId { get; set; }

    public string Number { get; set; }

    public string UserId { get; set; }

    public string QuizId { get; set; }

    public string Change { get; set; }

    public decimal? From { get; set; }

    public decimal? To { get; set; }

    public string Reason { get; set; }
}
