using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Credentials;

/// <summary>
/// Production review P4-L4: deleting a passing quiz attempt revokes the quiz passed credential it awarded, as voiding it
/// does, so the credential never outlives the attempt the course item no longer counts.
/// </summary>
public sealed class CredentialDeletedAttemptTests
{
    [Fact]
    public async Task P4L4_DeletingAPassingAttempt_RevokesTheQuizCredential()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("exam-badge", new CredentialAwardRule { Kind = CredentialsConstants.RuleKinds.QuizPassed, TargetId = "final" });

        var attempt = await h.PassQuizAsync("ana");
        await h.RunQueuedAsync();
        Assert.False(Assert.Single(await h.AwardsAsync("ana")).IsRevoked);

        // Act
        await h.NotifyAsync(attempt, (observer, context) => observer.DeletedAsync(context));

        // Assert
        var award = Assert.Single(await h.AwardsAsync("ana"));

        Assert.True(award.IsRevoked);
        Assert.False(string.IsNullOrWhiteSpace(award.RevokedReason));
    }
}
