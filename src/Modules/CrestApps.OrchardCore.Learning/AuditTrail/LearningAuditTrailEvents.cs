using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Learning.AuditTrail;

/// <summary>
/// The Audit Trail events of Learning: the changes people make by hand, each with its reason, and every export of
/// learner records. The Learning records stay the compliance record; the Audit Trail is a second view of them.
/// </summary>
internal sealed class LearningAuditTrailEvents : IConfigureOptions<AuditTrailOptions>
{
    public const string StatusChanged = "LearningEnrollmentStatusChanged";
    public const string AccessChanged = "LearningEnrollmentDatesChanged";
    public const string ItemCompletedByHand = "LearningItemCompletedByHand";
    public const string LearnerRecordsExported = "LearningLearnerRecordsExported";

    public void Configure(AuditTrailOptions options)
    {
        options.For<LearningAuditTrailEvents>(LearningConstants.AuditTrailCategory, S => S["Learning"])
            .WithEvent(StatusChanged, S => S["Enrollment status changed"], S => S["Someone approved, rejected, suspended, completed or withdrew an enrollment, with a reason."], enableByDefault: true)
            .WithEvent(AccessChanged, S => S["Enrollment dates changed"], S => S["Someone changed when an enrollment's access ends or when it is due, with a reason."], enableByDefault: true)
            .WithEvent(ItemCompletedByHand, S => S["Course item completed by hand"], S => S["Someone marked a learner's course item complete, with a reason."], enableByDefault: true)
            .WithEvent(LearnerRecordsExported, S => S["Learner records exported"], S => S["Someone exported learner records, which hold personal data."], enableByDefault: true);
    }
}
