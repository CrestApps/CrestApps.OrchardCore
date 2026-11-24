using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Phase 8, slice 5a: the rules that tell whether a completed enrollment may support what the site issues on it moved
/// from Certifications to Learning, their owner, so certificates and digital credentials apply the same rules.
/// </summary>
public sealed class CompletionEvidenceTests
{
    [Fact]
    public void RestsOnABrowserResult_ARequiredItemReportedByTheBrowser_OnAnItemThatDoesNotAllowIt_IsTrue()
    {
        // Arrange
        var enrollment = Completed(Item(required: true, resultSelfReported: true, allowSelfReported: false));

        // Act
        var rests = CompletionEvidence.RestsOnABrowserResult(enrollment);

        // Assert
        Assert.True(rests);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void RestsOnABrowserResult_IsFalse_WhenTheItemAllowsIt_IsOptional_OrWasComputedOnTheServer(bool required, bool resultSelfReported, bool allowSelfReported)
    {
        // Arrange
        var enrollment = Completed(Item(required, resultSelfReported, allowSelfReported));

        // Act
        var rests = CompletionEvidence.RestsOnABrowserResult(enrollment);

        // Assert
        Assert.False(rests);
    }

    [Fact]
    public void RestsOnABrowserResult_APageViewedWithoutAKnownLength_IsNotAResult()
    {
        // Arrange: SelfReported is also set for a page or a video viewed without a known length; only the result counts.
        var item = Item(required: true, resultSelfReported: false, allowSelfReported: false);
        item.SelfReported = true;

        // Act
        var rests = CompletionEvidence.RestsOnABrowserResult(Completed(item));

        // Assert
        Assert.False(rests);
    }

    [Fact]
    public void RestsOnABrowserResult_NoSnapshot_IsFalse()
    {
        // Act and assert: enrollments completed before snapshots kept the mark keep counting.
        Assert.False(CompletionEvidence.RestsOnABrowserResult(new Enrollment()));
        Assert.False(CompletionEvidence.RestsOnABrowserResult((Enrollment)null));
        Assert.False(CompletionEvidence.RestsOnABrowserResult((CompletionSnapshot)null));
    }

    [Fact]
    public void CountsAsEvidence_AFailedEnrollment_OrOneUnderReview_DoesNotCount()
    {
        // Arrange
        var failed = new Enrollment { Passed = false };
        var reviewed = new Enrollment { Review = new EnrollmentReview { Reason = LearningReviewReasons.ResultChangedAfterCompletion } };
        var cleared = new Enrollment { Review = new EnrollmentReview { Reason = LearningReviewReasons.ResultChangedAfterCompletion, ClearedUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc) } };
        var passed = new Enrollment { Passed = true };

        // Act and assert
        Assert.False(CompletionEvidence.CountsAsEvidence(failed));
        Assert.False(CompletionEvidence.CountsAsEvidence(reviewed));
        Assert.False(CompletionEvidence.CountsAsEvidence(null));
        Assert.True(CompletionEvidence.CountsAsEvidence(cleared));
        Assert.True(CompletionEvidence.CountsAsEvidence(passed));
        Assert.True(CompletionEvidence.CountsAsEvidence(new Enrollment()));
    }

    private static Enrollment Completed(CompletionSnapshotItem item)
        => new()
        {
            Status = EnrollmentStatus.Completed,
            Completion = new CompletionSnapshot
            {
                Items = [item],
            },
        };

    private static CompletionSnapshotItem Item(bool required, bool resultSelfReported, bool allowSelfReported)
        => new()
        {
            ContentItemId = "package",
            Required = required,
            State = CourseItemState.Completed,
            ResultSelfReported = resultSelfReported,
            AllowSelfReported = allowSelfReported,
        };
}
