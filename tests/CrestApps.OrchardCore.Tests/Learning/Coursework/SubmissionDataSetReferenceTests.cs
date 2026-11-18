using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Coursework.Reports;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// The Submissions data set names what its identifiers point at, so a report joins the course, the coursework, the
/// learner and the grader to show their titles and names instead of raw ids.
/// </summary>
public sealed class SubmissionDataSetReferenceTests
{
    [Fact]
    public void TheCourseCourseworkLearnerAndGraderIds_ReferenceWhatTheyIdentify()
    {
        // Arrange
        IRecordDataSet submissions = new SubmissionReportDataSet(Mock.Of<ISession>(), Mock.Of<IAuthorizationService>(), new PassThroughStringLocalizer<SubmissionReportDataSet>());

        // Act
        DataFieldReference Reference(string field)
            => Assert.Single(submissions.Fields.Single(candidate => candidate.Name == field).References ?? []);

        // Assert
        Assert.Equal(LearningConstants.ContentTypes.Course, Reference("CourseId").DataSet);
        Assert.Equal(CourseworkConstants.ContentTypes.Coursework, Reference("CourseworkId").DataSet);
        Assert.NotNull(Reference("UserId"));
        Assert.NotNull(Reference("GraderId"));
    }
}
