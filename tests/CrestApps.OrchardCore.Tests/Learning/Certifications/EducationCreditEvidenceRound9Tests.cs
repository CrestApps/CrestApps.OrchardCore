using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Certifications;

/// <summary>
/// Review round 9, H5: a course completion that is no evidence earns no education credits, because credits meet the
/// credits requirement of a certification. A completion resting on a result the browser reported (a learning package
/// whose status the learner can set from the console) earns nothing unless its item lets such a result count; a failed
/// completion and one under review earn nothing either.
/// </summary>
public sealed class EducationCreditEvidenceRound9Tests
{
    [Fact]
    public async Task Round9H5_ACompletionRestingOnABrowserResult_AwardsNoCredits_UnlessTheItemAllowsIt()
    {
        // Arrange: ana's course has a package whose result the browser reports; ben's lets such a result count.
        CertificationHarness h = null;
        h = await CertificationHarness.CreateAsync(configure: services =>
        {
            var content = new Mock<IContentManager>();

            content.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
                .ReturnsAsync((string id, VersionOptions _) => id is "strict" or "lenient" ? h.Inner.Courses.Get(id) : null);
            services.RemoveAll<IContentManager>();
            services.AddSingleton(content.Object);
        });

        var cpd = new EducationCreditType { Name = "CPD hours", Unit = "hours" };

        Assert.Empty(await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SaveTypeAsync(cpd)));

        h.Inner.Courses.AddCourse("strict");
        h.Inner.Courses.AddItem("strict", "package-strict", "Package", CompletionModes.PackageReported);
        h.Inner.Courses.AddCourse("lenient");
        h.Inner.Courses.AddItem("lenient", "package-lenient", "Package", CompletionModes.PackageReported, configure: part => part.AllowSelfReported = true);

        foreach (var course in new[] { "strict", "lenient" })
        {
            h.Inner.Courses.Get(course).Alter<EducationCreditsPart>(part => part.Awards = [new EducationCreditAward { CreditTypeId = cpd.ItemId, Amount = 2 }]);
        }

        // Act
        await ReportFromTheBrowserAsync(h, "ana", "strict", "package-strict");
        await ReportFromTheBrowserAsync(h, "ben", "lenient", "package-lenient");

        var ana = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().GetBalancesAsync("ana", CertificationHarness.Today));
        var ben = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().GetBalancesAsync("ben", CertificationHarness.Today));

        // Assert
        Assert.Equal(0, ana.GetValueOrDefault(cpd.ItemId));
        Assert.Equal(2, ben.GetValueOrDefault(cpd.ItemId));
    }

    private static async Task ReportFromTheBrowserAsync(CertificationHarness h, string userId, string courseId, string itemId)
    {
        var enrollment = await h.Inner.EnrollAsync(userId, courseId);

        await h.RunAsync(async services =>
        {
            var fresh = await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId);
            var update = await services.GetRequiredService<ILearnerProgressService>().RecordResultAsync(fresh, new CourseItemResult { ItemId = itemId, ReportedComplete = true, SelfReported = true });

            Assert.True(update.CourseCompleted);
        });
    }
}
