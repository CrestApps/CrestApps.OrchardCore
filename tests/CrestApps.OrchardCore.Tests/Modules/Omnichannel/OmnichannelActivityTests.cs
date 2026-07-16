using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class OmnichannelActivityTests
{
    private static readonly DateTime _resolvedUtc = new(2026, 7, 14, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TryResolveContact_WhenCandidateIsSelected_RecordsAuditedResolution()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ContactResolutionStatus = ContactResolutionStatus.Ambiguous,
            ContactResolutionCandidates =
            [
                "contact-a",
                "contact-b",
            ],
        };
        var contact = new ContentItem
        {
            ContentItemId = "contact-b",
            ContentType = "Customer",
        };

        // Act
        var resolved = activity.TryResolveContact(
            contact,
            "user-id",
            "Agent User",
            _resolvedUtc);

        // Assert
        Assert.True(resolved);
        Assert.Equal(ContactResolutionStatus.Resolved, activity.ContactResolutionStatus);
        Assert.Equal("contact-b", activity.ContactContentItemId);
        Assert.Equal("Customer", activity.ContactContentType);
        Assert.Equal("user-id", activity.ContactResolvedById);
        Assert.Equal("Agent User", activity.ContactResolvedByUsername);
        Assert.Equal(_resolvedUtc, activity.ContactResolvedUtc);
        Assert.Equal(["contact-a", "contact-b"], activity.ContactResolutionCandidates);
    }

    [Fact]
    public void TryResolveContact_WhenContactIsNotCandidate_LeavesAmbiguityUnchanged()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ContactResolutionStatus = ContactResolutionStatus.Ambiguous,
            ContactResolutionCandidates =
            [
                "contact-a",
                "contact-b",
            ],
        };
        var contact = new ContentItem
        {
            ContentItemId = "contact-c",
            ContentType = "Customer",
        };

        // Act
        var resolved = activity.TryResolveContact(
            contact,
            "user-id",
            "Agent User",
            _resolvedUtc);

        // Assert
        Assert.False(resolved);
        Assert.Equal(ContactResolutionStatus.Ambiguous, activity.ContactResolutionStatus);
        Assert.Null(activity.ContactContentItemId);
        Assert.Null(activity.ContactContentType);
        Assert.Null(activity.ContactResolvedById);
        Assert.Null(activity.ContactResolvedByUsername);
        Assert.Null(activity.ContactResolvedUtc);
    }

    [Fact]
    public void TryResolveContact_WhenSameContactWasAlreadyResolved_IsIdempotent()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ContactResolutionStatus = ContactResolutionStatus.Resolved,
            ContactContentItemId = "contact-a",
            ContactContentType = "Customer",
            ContactResolvedById = "original-user",
            ContactResolvedByUsername = "Original User",
            ContactResolvedUtc = _resolvedUtc,
        };
        var contact = new ContentItem
        {
            ContentItemId = "contact-a",
            ContentType = "Customer",
        };

        // Act
        var resolved = activity.TryResolveContact(
            contact,
            "second-user",
            "Second User",
            _resolvedUtc.AddMinutes(1));

        // Assert
        Assert.True(resolved);
        Assert.Equal("original-user", activity.ContactResolvedById);
        Assert.Equal("Original User", activity.ContactResolvedByUsername);
        Assert.Equal(_resolvedUtc, activity.ContactResolvedUtc);
    }

    [Fact]
    public void TryResolveContact_WhenDifferentContactWasAlreadyResolved_RejectsReattribution()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ContactResolutionStatus = ContactResolutionStatus.Resolved,
            ContactContentItemId = "contact-a",
            ContactContentType = "Customer",
            ContactResolvedById = "original-user",
            ContactResolvedByUsername = "Original User",
            ContactResolvedUtc = _resolvedUtc,
        };
        var contact = new ContentItem
        {
            ContentItemId = "contact-b",
            ContentType = "Customer",
        };

        // Act
        var resolved = activity.TryResolveContact(
            contact,
            "second-user",
            "Second User",
            _resolvedUtc.AddMinutes(1));

        // Assert
        Assert.False(resolved);
        Assert.Equal("contact-a", activity.ContactContentItemId);
        Assert.Equal("original-user", activity.ContactResolvedById);
        Assert.Equal("Original User", activity.ContactResolvedByUsername);
        Assert.Equal(_resolvedUtc, activity.ContactResolvedUtc);
    }
}
