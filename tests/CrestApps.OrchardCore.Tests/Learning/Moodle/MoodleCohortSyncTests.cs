using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Moodle;
using CrestApps.OrchardCore.Learning.Core.Moodle;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Learning.Moodle;

/// <summary>
/// Moodle's cohort sync enrolment becomes the course's cohort sync: the import matches a cohort of the site by the
/// Moodle cohort's id number or id, or by the enrolment's or cohort's name, and reports a cohort the site does not have.
/// </summary>
public sealed class MoodleCohortSyncTests
{
    [Fact]
    public async Task TheReader_ReadsTheCohortOfACohortSyncEnrolment()
    {
        // Arrange
        var builder = new MoodleBackupBuilder(91, "Cohort course").Section(1, 0, null).Enrolment("manual").Cohort(17, "Field crew", "FIELD");
        using var folder = new TempFolder();

        // Act
        var result = await MoodleBackupReader.ExtractAndReadAsync(new MemoryStream(builder.BuildZip()), folder.Path, new MoodleBackupLimits(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        var cohort = Assert.Single(result.Backup.Course.Enrolments, enrolment => enrolment.Method == "cohort");
        Assert.Equal("17", cohort.CohortId);
        Assert.Equal("FIELD", cohort.CohortIdNumber);
        Assert.Equal("Field crew", cohort.CohortName);
    }

    [Fact]
    public void ACohortSync_IsMatchedByIdNumberIdOrName()
    {
        // Arrange
        var enrolment = new MoodleEnrolment { Method = "cohort", Enabled = true, CohortId = "17", CohortIdNumber = "FIELD", CohortName = "Field crew", Name = "Crew sync" };
        IReadOnlyList<(string Id, string Name)> byName = [("other", "Office"), ("c1", "Field crew")];
        IReadOnlyList<(string Id, string Name)> byIdNumber = [("FIELD", "Field team"), ("c1", "Field crew")];

        // Act and assert
        Assert.Equal("c1", MoodleCohorts.Match(enrolment, byName));
        Assert.Equal("FIELD", MoodleCohorts.Match(enrolment, byIdNumber));
        Assert.Null(MoodleCohorts.Match(enrolment, [("x", "Somebody else")]));
        Assert.Equal("c2", MoodleCohorts.Match(new MoodleEnrolment { Method = "cohort", Name = "Crew sync" }, [("c2", "crew sync")]));
    }

    [Fact]
    public void TheRows_ReportOnlyACohortTheSiteDoesNotHave_OrCohortsTurnedOff()
    {
        // Arrange
        var backup = new MoodleBackup
        {
            SiteKey = "abc",
            Course = new MoodleCourse
            {
                Id = "5",
                FullName = "Cohort course",
                Enrolments =
                [
                    new MoodleEnrolment { Method = "cohort", Enabled = true, CohortId = "1", CohortName = "Field crew" },
                    new MoodleEnrolment { Method = "cohort", Enabled = true, CohortId = "2", CohortName = "Night shift" },
                ],
            },
        };
        var localizer = new Mock<IStringLocalizer>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
        localizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] arguments) => new LocalizedString(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments)));

        // Act
        var rows = MoodleCourseRows.Write(backup, "token", new QuestionTypeOptions(), localizer.Object, cohorts: [("c1", "Field crew")]);
        var off = MoodleCourseRows.Write(backup, "token", new QuestionTypeOptions(), localizer.Object, cohorts: null);

        // Assert
        var errors = rows.Select(row => row[5]).Where(error => !string.IsNullOrEmpty(error)).ToList();
        var message = Assert.Single(errors);
        Assert.Contains("Night shift", message, StringComparison.Ordinal);
        Assert.Equal(2, off.Count(row => row[5]?.Contains("cohort", StringComparison.OrdinalIgnoreCase) == true));
    }
}
