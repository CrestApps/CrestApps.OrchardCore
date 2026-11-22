using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// A survey's anonymity secret is never replaced on its own: when the Data Protection keys that protected it are
/// missing, the survey fails closed, and only the explicit reset replaces it.
/// </summary>
public sealed class SurveyAnonymitySecretsTests
{
    [Fact]
    public async Task RevealAsync_SecretProtectedWithOtherKeys_FailsClosedAndKeepsTheSecret()
    {
        // Arrange: the secret was protected on another host, whose keys this node doesn't have.
        var survey = new Survey { ItemId = "s1", Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous } };
        Secrets(new EphemeralDataProtectionProvider(), new Mock<ISession>(MockBehavior.Strict)).Assign(survey);
        var stored = survey.AnonymitySecret;
        var session = new Mock<ISession>(MockBehavior.Strict);
        var secrets = Secrets(new EphemeralDataProtectionProvider(), session);

        // Act
        var exception = await Assert.ThrowsAsync<SurveyAnonymitySecretUnreadableException>(() => secrets.RevealAsync(survey, TestContext.Current.CancellationToken));

        // Assert: nothing was saved and the stored secret is the one the keys can still open once restored.
        Assert.Equal("s1", exception.SurveyId);
        Assert.Equal(stored, survey.AnonymitySecret);
        Assert.False(secrets.CanReveal(survey));
        session.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RespondentKeyAsync_SecretProtectedWithOtherKeys_FailsClosed()
    {
        // Arrange
        var survey = new Survey { ItemId = "s1", Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous } };
        Secrets(new EphemeralDataProtectionProvider(), new Mock<ISession>(MockBehavior.Strict)).Assign(survey);
        var secrets = Secrets(new EphemeralDataProtectionProvider(), new Mock<ISession>(MockBehavior.Strict));

        // Act and Assert: no respondent key is made from a new secret, so limits don't silently start over.
        await Assert.ThrowsAsync<SurveyAnonymitySecretUnreadableException>(() => secrets.RespondentKeyAsync(survey, new SurveyRespondentIdentity { UserId = "u1" }, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevealAsync_SecretProtectedWithTheseKeys_ReturnsTheSameSecretEveryTime()
    {
        // Arrange
        var provider = new EphemeralDataProtectionProvider();
        var survey = new Survey { ItemId = "s1" };
        var secrets = Secrets(provider, new Mock<ISession>(MockBehavior.Strict));
        secrets.Assign(survey);

        // Act
        var first = await secrets.RevealAsync(survey, TestContext.Current.CancellationToken);
        var second = await secrets.RevealAsync(survey, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(secrets.CanReveal(survey));
        Assert.Equal(first, second);
    }

    private static SurveyAnonymitySecrets Secrets(IDataProtectionProvider provider, Mock<ISession> session)
        => new(provider, new SurveyStore(session.Object), NullLogger<SurveyAnonymitySecrets>.Instance);
}
