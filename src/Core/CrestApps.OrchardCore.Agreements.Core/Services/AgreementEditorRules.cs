using CrestApps.OrchardCore.Agreements.Models;

namespace CrestApps.OrchardCore.Agreements.Core.Services;

/// <summary>
/// The rules the agreement's editor applies to what is posted, kept in one place for every writer of an agreement's own
/// fields: the Agreements editor's drivers and the tools that change agreements without the editor (the service
/// agreement AI tools), so they all apply a change the same way. What an agreement may hold is checked afterwards by its
/// handler, when the manager validates it.
/// </summary>
public static class AgreementEditorRules
{
    /// <summary>
    /// Applies the agreement's general fields as its editor does: the name and the notes are trimmed, an empty account,
    /// time zone or notes is none, and a new end date or a status other than Active gives the agreement a new notice
    /// before its end.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="fields">The fields posted.</param>
    public static void ApplyGeneralFields(Agreement agreement, AgreementGeneralFields fields)
    {
        ArgumentNullException.ThrowIfNull(agreement);
        ArgumentNullException.ThrowIfNull(fields);

        // A new end date, or a pause, gives the agreement a new notice.
        if (agreement.EndDate != fields.EndDate || fields.Status != AgreementStatus.Active)
        {
            agreement.ExpiringNotifiedUtc = null;
        }

        agreement.Name = fields.Name?.Trim();
        agreement.Status = fields.Status;
        agreement.AccountId = string.IsNullOrWhiteSpace(fields.AccountId) ? null : fields.AccountId.Trim();
        agreement.StartDate = fields.StartDate;
        agreement.EndDate = fields.EndDate;
        agreement.TimeZoneId = string.IsNullOrWhiteSpace(fields.TimeZoneId) ? null : fields.TimeZoneId;
        agreement.ExpiringNoticeDays = fields.ExpiringNoticeDays;
        agreement.Notes = string.IsNullOrWhiteSpace(fields.Notes) ? null : fields.Notes.Trim();
    }

    /// <summary>
    /// Applies the hours an agreement covers as its editor does: an agreement that covers no hours keeps none.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="kind">How it covers hours.</param>
    /// <param name="hours">The hours of the block, or of each month.</param>
    /// <param name="roundingMinutes">What each entry of time is rounded up to, in minutes.</param>
    public static void ApplyHours(Agreement agreement, AgreementHoursKind kind, decimal hours, int roundingMinutes)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        agreement.Hours = new AgreementHours
        {
            Kind = kind,
            Hours = kind == AgreementHoursKind.None ? 0 : hours,
            RoundingMinutes = roundingMinutes,
        };
    }

    /// <summary>
    /// Tells whether an agreement can be deleted: only a draft can; any other is canceled instead.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <returns><see langword="true"/> when it can be deleted.</returns>
    public static bool CanDelete(Agreement agreement)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        return agreement.Status == AgreementStatus.Draft;
    }
}
