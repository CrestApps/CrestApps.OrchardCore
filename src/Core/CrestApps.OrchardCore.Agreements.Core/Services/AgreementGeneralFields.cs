using CrestApps.OrchardCore.Agreements.Models;

namespace CrestApps.OrchardCore.Agreements.Core.Services;

/// <summary>
/// The general fields of an agreement as its editor posts them, for <see cref="AgreementEditorRules.ApplyGeneralFields"/>.
/// </summary>
public sealed class AgreementGeneralFields
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public AgreementStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the account it is with.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets its first day.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets its last day, or <see langword="null"/> for no end.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the time zone its days are counted in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets how many days before its end the notice comes.
    /// </summary>
    public int ExpiringNoticeDays { get; set; }

    /// <summary>
    /// Gets or sets the notes for the people who work under it.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Reads an agreement's general fields, as its editor shows them.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <returns>The fields.</returns>
    public static AgreementGeneralFields From(Agreement agreement)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        return new AgreementGeneralFields
        {
            Name = agreement.Name,
            Status = agreement.Status,
            AccountId = agreement.AccountId,
            StartDate = agreement.StartDate,
            EndDate = agreement.EndDate,
            TimeZoneId = agreement.TimeZoneId,
            ExpiringNoticeDays = agreement.ExpiringNoticeDays,
            Notes = agreement.Notes,
        };
    }
}
