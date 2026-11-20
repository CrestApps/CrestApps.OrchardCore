namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Every page of "Your account" is framed alike: a new learner with nothing yet sees the account's empty-state card on
/// each Learning page and on Notifications, as on Orders, instead of a bare line of text.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class AccountEmptyStateTests
{
    private readonly LearningSiteFixture _fixture;

    public AccountEmptyStateTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("learning")]
    [InlineData("learning/transcript")]
    [InlineData("learning/certifications")]
    [InlineData("learning/competencies")]
    [InlineData("learning/credits")]
    [InlineData("learning/credentials")]
    [InlineData("learning/required")]
    [InlineData("notifications")]
    public async Task ANewLearner_SeesTheAccountEmptyState_OnEveryAccountPage(string path)
    {
        // Arrange
        var userName = "empty" + Guid.NewGuid().ToString("n")[..8];
        await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, userName));
        using var client = await _fixture.CreateClientAsync(userName);

        // Act
        var page = await client.GetStringAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("commerce-account", page);
        Assert.Contains("data-account-empty", page);
    }
}
