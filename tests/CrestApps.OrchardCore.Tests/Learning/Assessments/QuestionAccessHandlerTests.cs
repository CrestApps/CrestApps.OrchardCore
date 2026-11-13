using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// A role that holds the general View content permission must still not read a question or a bank: only people who
/// author questions, see answer keys or grade may.
/// </summary>
public sealed class QuestionAccessHandlerTests
{
    [Fact]
    public async Task ViewContent_OnAQuestionOrBank_FailsForEveryoneButAuthorsAndGraders()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        var question = h.Questions.AddChoice("q1", "a");
        var bank = new ContentItem { ContentItemId = "bank", ContentType = AssessmentsConstants.ContentTypes.QuestionBank };
        var page = new ContentItem { ContentItemId = "page", ContentType = "Page" };

        // Act
        Task<AuthorizationHandlerContext> Check(ClaimsPrincipal user, ContentItem item, string permission = null)
            => h.RunAsync(async services =>
            {
                var handler = ActivatorUtilities.CreateInstance<QuestionContentAccessHandler>(services);
                var requirement = new PermissionRequirement(permission == "preview" ? CommonPermissions.PreviewContent : CommonPermissions.ViewContent);
                var context = new AuthorizationHandlerContext([requirement], user, item);

                await handler.HandleAsync(context);

                return context;
            });

        var reader = await Check(CalendarTestHarness.User("reader", CommonPermissions.ViewContent.Name), question);
        var readerBank = await Check(CalendarTestHarness.User("reader", CommonPermissions.ViewContent.Name), bank);
        var readerPreview = await Check(CalendarTestHarness.User("reader", CommonPermissions.PreviewContent.Name), question, "preview");
        var author = await Check(CalendarTestHarness.User("author", AssessmentPermissions.ManageQuestionBanks.Name), question);
        var grader = await Check(CalendarTestHarness.User("grader", AssessmentPermissions.GradeAssessments.Name), question);
        var anonymous = await Check(new ClaimsPrincipal(new ClaimsIdentity()), question);
        var other = await Check(CalendarTestHarness.User("reader", CommonPermissions.ViewContent.Name), page);

        // Assert
        Assert.True(reader.HasFailed);
        Assert.True(readerBank.HasFailed);
        Assert.True(readerPreview.HasFailed);
        Assert.True(anonymous.HasFailed);
        Assert.True(author.HasSucceeded);
        Assert.True(grader.HasSucceeded);
        Assert.False(other.HasFailed);
        Assert.False(other.HasSucceeded);
    }
}
