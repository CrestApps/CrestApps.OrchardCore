using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;

namespace CrestApps.OrchardCore.Tests.Agreements;

/// <summary>
/// The agreement editor's rules, which its drivers and the service agreement AI tools share: the general fields are
/// trimmed and an empty one is none, a new end date or a status other than Active gives a new notice, an agreement that
/// covers no hours keeps none, and only a draft can be deleted.
/// </summary>
public sealed class AgreementEditorRulesTests
{
    private static readonly DateTime _notified = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheGeneralFields_AreTrimmed_AndAnEmptyAccountTimeZoneOrNotesIsNone()
    {
        // Arrange
        var agreement = new Agreement { AccountId = "old", TimeZoneId = "Europe/Paris", Notes = "Old" };

        // Act
        AgreementEditorRules.ApplyGeneralFields(agreement, new AgreementGeneralFields
        {
            Name = "  Gold support  ",
            Status = AgreementStatus.Draft,
            AccountId = "   ",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            TimeZoneId = " ",
            ExpiringNoticeDays = 45,
            Notes = "  ",
        });

        // Assert
        Assert.Equal("Gold support", agreement.Name);
        Assert.Null(agreement.AccountId);
        Assert.Null(agreement.TimeZoneId);
        Assert.Null(agreement.Notes);
        Assert.Equal(new DateOnly(2026, 12, 31), agreement.EndDate);
        Assert.Equal(45, agreement.ExpiringNoticeDays);
    }

    [Fact]
    public void ANewEndDateOrAStatusOtherThanActive_GivesANewNotice_AndAnUnchangedActiveAgreementKeepsIt()
    {
        // Arrange
        var unchanged = Active();
        var newEnd = Active();
        var paused = Active();

        // Act
        AgreementEditorRules.ApplyGeneralFields(unchanged, AgreementGeneralFields.From(unchanged));
        AgreementEditorRules.ApplyGeneralFields(newEnd, Changed(newEnd, fields => fields.EndDate = new DateOnly(2027, 6, 30)));
        AgreementEditorRules.ApplyGeneralFields(paused, Changed(paused, fields => fields.Status = AgreementStatus.Paused));

        // Assert
        Assert.Equal(_notified, unchanged.ExpiringNotifiedUtc);
        Assert.Null(newEnd.ExpiringNotifiedUtc);
        Assert.Null(paused.ExpiringNotifiedUtc);
    }

    [Fact]
    public void AnAgreementThatCoversNoHours_KeepsNone_AndOnlyADraftCanBeDeleted()
    {
        // Arrange
        var agreement = new Agreement();

        // Act
        AgreementEditorRules.ApplyHours(agreement, AgreementHoursKind.None, 12, 15);

        // Assert
        Assert.Equal(AgreementHoursKind.None, agreement.Hours.Kind);
        Assert.Equal(0m, agreement.Hours.Hours);
        Assert.Equal(15, agreement.Hours.RoundingMinutes);
        Assert.True(AgreementEditorRules.CanDelete(new Agreement { Status = AgreementStatus.Draft }));
        Assert.False(AgreementEditorRules.CanDelete(new Agreement { Status = AgreementStatus.Canceled }));
    }

    private static Agreement Active()
        => new()
        {
            Name = "Gold",
            Status = AgreementStatus.Active,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            ExpiringNotifiedUtc = _notified,
        };

    private static AgreementGeneralFields Changed(Agreement agreement, Action<AgreementGeneralFields> change)
    {
        var fields = AgreementGeneralFields.From(agreement);

        change(fields);

        return fields;
    }
}
